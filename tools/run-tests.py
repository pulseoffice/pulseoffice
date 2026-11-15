#!/usr/bin/env python3
import argparse
import os
import random
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
GLOBAL_FILES = {"Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "PulseOffice.slnx",
                "global.json", "nuget.config", "NuGet.Config", ".editorconfig"}

DESCRIPTION = """Builds with few MSBuild nodes and no resident compiler server, then runs the test projects one at a time.
Each project is split into chunks of test classes; on Linux every chunk runs in its own systemd scope with a hard
memory cap, so a runaway chunk is killed alone instead of the whole session. Results and the memory peak of each
chunk go to summary.tsv.

Environment: TESTS_MEMORY_MAX (default 6G), TESTS_CLASSES_PER_CHUNK (default 40), TESTS_BUILD_NODES (default 2),
TESTS_OUT (default artifacts/test-runs/<timestamp>)."""


class ProjectGraph:
    def __init__(self, repo):
        self.repo = repo
        self.projects = [path.resolve() for root in ("src", "tests") for path in (repo / root).rglob("*.csproj")
                         if not {"bin", "obj"} & set(path.relative_to(repo).parts)]
        self.tests = sorted((project for project in self.projects
                             if project.parent.parent == (repo / "tests").resolve() and project.stem.endswith(".Tests")),
                            key=lambda project: project.stem)
        self.dependants = {project: set() for project in self.projects}
        self.linked = {}
        for project in self.projects:
            for reference in self._items(project, "ProjectReference"):
                target = self._resolve(project, reference)
                if target in self.dependants:
                    self.dependants[target].add(project)
            for include in self._items(project, "Compile") + self._items(project, "Content") + self._items(project, "None"):
                if "*" not in include and ".." in include:
                    self.linked.setdefault(self._resolve(project, include), set()).add(project)

    @staticmethod
    def _items(project, tag):
        try:
            tree = ET.parse(project)
        except ET.ParseError:
            return []
        return [element.get("Include") for element in tree.iter()
                if element.tag.split("}")[-1] == tag and element.get("Include")]

    @staticmethod
    def _resolve(project, include):
        return (project.parent / include.replace("\\", "/")).resolve()

    def affected_tests(self, files):
        touched = set()
        for file in files:
            if file.name in GLOBAL_FILES and file.parent == self.repo:
                return list(self.tests)
            touched |= self.linked.get(file, set())
            owner = next((project for project in self.projects if project.parent in file.parents), None)
            if owner is not None:
                touched.add(owner)
        pending = list(touched)
        while pending:
            for dependant in self.dependants.get(pending.pop(), ()):
                if dependant not in touched:
                    touched.add(dependant)
                    pending.append(dependant)
        return [project for project in self.tests if project in touched]


class ChangedFiles:
    def __init__(self, repo):
        self.repo = repo

    def since(self, base):
        names = set()
        for command in (["git", "diff", "--name-only", f"{base}...HEAD"], ["git", "diff", "--name-only", "HEAD"],
                        ["git", "ls-files", "--others", "--exclude-standard"]):
            result = subprocess.run(command, cwd=self.repo, capture_output=True, text=True, check=True)
            names.update(line for line in result.stdout.splitlines() if line)
        return [(self.repo / name).resolve() for name in names]


class MemoryCap:
    def __init__(self, limit):
        self.limit = limit
        self.available = sys.platform.startswith("linux") and shutil.which("systemd-run") is not None

    def wrap(self, command):
        if not self.available:
            return command
        return ["systemd-run", "--user", "--scope", "--quiet", "--collect", "-p", f"MemoryMax={self.limit}",
                "-p", "MemorySwapMax=0", f"--unit=pulse-tests-{os.getpid()}-{random.randrange(1 << 16)}", "--"] + command

    def peak_probe(self, command):
        if not self.available:
            return command
        script = ('"$@"; code=$?; echo "PEAK=$(cat /sys/fs/cgroup$(cut -d: -f3 /proc/self/cgroup)/memory.peak)"; '
                  'exit $code')
        return ["bash", "-c", script, "probe"] + command


class TestRunner:
    def __init__(self, options, cap):
        self.options = options
        self.cap = cap
        self.out = Path(options.out)
        self.summary = self.out / "summary.tsv"

    def environment(self):
        env = dict(os.environ)
        env.update({"DOTNET_gcServer": "0", "DOTNET_GCConserveMemory": "7", "DOTNET_TieredPGO": "0",
                    "MSBUILDDISABLENODEREUSE": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
        return env

    def build(self, targets):
        log = self.out / "build.log"
        print(f"building {len(targets)} target(s) with {self.options.nodes} nodes")
        with log.open("w") as stream:
            for target in targets:
                command = ["dotnet", "build", str(target), "-nologo", "-v", "q", f"-m:{self.options.nodes}",
                           "-nodeReuse:false", "-p:UseSharedCompilation=false"]
                code = subprocess.run(self.cap.wrap(command), stdout=stream, stderr=subprocess.STDOUT,
                                      env=self.environment()).returncode
                if code != 0:
                    break
        subprocess.run(["dotnet", "build-server", "shutdown"], capture_output=True, env=self.environment())
        print("".join(log.read_text(errors="replace").splitlines(keepends=True)[-3:]), end="")
        if code != 0:
            sys.exit(f"build failed, see {log}")

    def classes(self, project):
        result = subprocess.run(["dotnet", "test", str(project), "--no-build", "--list-tests", "-nologo"],
                                capture_output=True, text=True, env=self.environment())
        names = set()
        for line in result.stdout.splitlines():
            if line.startswith("    "):
                parts = line.strip().split("(")[0].split(".")
                if len(parts) > 1:
                    names.add(".".join(parts[:-1]))
        return sorted(names)

    def run_chunk(self, project, index, test_filter):
        log = self.out / f"{project.stem}.{index}.log"
        command = ["dotnet", "test", str(project), "--no-build", "-nologo"]
        if test_filter:
            command += ["--filter", test_filter]
        command += ["--", "RunConfiguration.MaxCpuCount=1"]
        started = time.monotonic()
        with log.open("w") as stream:
            code = subprocess.run(self.cap.wrap(self.cap.peak_probe(command)), stdout=stream,
                                  stderr=subprocess.STDOUT, env=self.environment()).returncode
        seconds = int(time.monotonic() - started)
        text = log.read_text(errors="replace")
        passed = self._last(r"(?:Superati|Passed):\s+(\d+)", text)
        failed = self._last(r"(?:Non superati|Failed):\s+(\d+)", text)
        peak = self._last(r"PEAK=(\d+)", text)
        if code == 0:
            result = "ok"
        elif (self.cap.available and peak is None) or re.search(r"oom|Killed", text, re.IGNORECASE):
            result = "killed"
        else:
            result = "failed"
        peak_mb = int(peak or 0) // 1048576
        with self.summary.open("a") as stream:
            stream.write(f"{project.stem}\t{index}\t{result}\t{passed or 0}\t{failed or 0}\t{peak_mb}\t{seconds}\n")
        print(f"{project.stem:<48} {index:>3} {result:<7} pass {passed or 0:<5} fail {failed or 0:<4} "
              f"peak {peak_mb:>5} MB  {seconds:>4}s", flush=True)

    @staticmethod
    def _last(pattern, text):
        found = re.findall(pattern, text)
        return found[-1] if found else None

    def run(self, projects):
        self.out.mkdir(parents=True, exist_ok=True)
        self.summary.write_text("project\tchunk\tresult\tpassed\tfailed\tpeak_mb\tseconds\n")
        if not self.cap.available:
            print("warning: no memory cap on this system; chunks run without a limit")
        if self.options.build:
            self.build(projects if self.options.changed is not None else [REPO / "PulseOffice.slnx"])
        for project in projects:
            classes = self.classes(project)
            size = self.options.classes_per_chunk
            if len(classes) <= size:
                self.run_chunk(project, 1, "")
                continue
            for index, start in enumerate(range(0, len(classes), size), 1):
                self.run_chunk(project, index, "|".join(f"FullyQualifiedName~{name}." for name in classes[start:start + size]))
        return self.report()

    def report(self):
        rows = [line.split("\t") for line in self.summary.read_text().splitlines()[1:]]
        print(f"\nsummary: {self.summary}")
        bad = [row for row in rows if row[2] != "ok"]
        for row in bad:
            print(f"  {row[0]} chunk {row[1]}: {row[2]}")
        if rows:
            top = max(rows, key=lambda row: int(row[5]))
            print(f"highest peak: {top[5]} MB ({top[0]} chunk {top[1]})")
        return 1 if bad else 0


def main():
    parser = argparse.ArgumentParser(description=DESCRIPTION, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("fragments", nargs="*", help="run only test projects whose name contains one of these")
    parser.add_argument("--no-build", dest="build", action="store_false", help="use the existing build")
    parser.add_argument("--changed", nargs="?", const="main", metavar="BASE",
                        help="run only the test projects affected by files changed since BASE (default main), "
                             "including uncommitted ones; builds only those projects")
    parser.add_argument("--files", nargs="+", metavar="FILE", help="print the test projects affected by these files and exit")
    parser.add_argument("--list", action="store_true", help="print the selected test projects and exit")
    options = parser.parse_args()
    options.out = os.environ.get("TESTS_OUT") or str(REPO / "artifacts" / "test-runs" / datetime.now().strftime("%Y%m%d-%H%M%S"))
    options.nodes = int(os.environ.get("TESTS_BUILD_NODES", "2"))
    options.classes_per_chunk = int(os.environ.get("TESTS_CLASSES_PER_CHUNK", "40"))

    graph = ProjectGraph(REPO)
    if options.files:
        for project in graph.affected_tests([Path(name).resolve() for name in options.files]):
            print(project.stem)
        return 0
    projects = graph.affected_tests(ChangedFiles(REPO).since(options.changed)) if options.changed is not None else graph.tests
    if options.fragments:
        projects = [project for project in projects if any(fragment in project.stem for fragment in options.fragments)]
    if options.list:
        for project in projects:
            print(project.stem)
        return 0
    if not projects:
        print("no test project selected")
        return 0
    return TestRunner(options, MemoryCap(os.environ.get("TESTS_MEMORY_MAX", "6G"))).run(projects)


if __name__ == "__main__":
    sys.exit(main())
