#!/usr/bin/env python3
import argparse
import hashlib
import json
import math
import os
import random
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
import xml.etree.ElementTree as ET
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
GLOBAL_FILES = {"Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "PulseOffice.slnx",
                "global.json", "nuget.config", "NuGet.Config", ".editorconfig"}
UNCACHEABLE = {"PulseOffice.Architecture.Tests", "PulseOffice.Localization.Tests", "PulseOffice.Robustness.Tests"}
CONFIGURATION = "Debug"
FRAMEWORK = "net10.0"

DESCRIPTION = """Builds the selected test projects in one MSBuild invocation with few nodes and no resident compiler
server, then runs each test assembly directly with its own xUnit runner (no dotnet test, no vstest). Each project is
split into chunks of test classes; on Linux every chunk runs in its own systemd scope with a hard memory cap, so a
runaway chunk is killed alone instead of the whole session. Up to TESTS_JOBS chunks run at once, longest first. A
project whose built output has not changed since its last green run is skipped (result cache). Results, the memory
peak of each chunk and every test slower than TESTS_SLOW_SECONDS go to summary.tsv and slow.tsv.

A chunk running longer than TESTS_CHUNK_TIMEOUT (default 600 s) is killed and marked timeout; a chunk where one test
runs longer than TESTS_HANG_SECONDS (default 60 s, seen through xUnit's long-running reports) is killed and marked hang.
Both count as failures: a long test is stopped and fixed, never waited for. summary.tsv names the slowest test of
each chunk.

Chunks are balanced by the per-class durations recorded in the cache, aiming at TESTS_CHUNK_SECONDS each, so a long
serial project (one headless UI collection) is spread over several processes.

Each chunk's MemoryMax is 1.5 times the highest memory peak recorded in the cache for its project (1 to 6 GB; 4 GB
while none is recorded), and a chunk starts only when its cap fits beside those of the running chunks within
TESTS_MEMORY_TOTAL_GB.

Environment: TESTS_JOBS (chunks at once, default 5), TESTS_MEMORY_TOTAL_GB (budget shared by the running chunks,
default 12), TESTS_MEMORY_MAX (one fixed cap for every chunk, such as 3G), TESTS_CHUNK_SECONDS
(default 60), TESTS_CLASSES_PER_CHUNK (default 40), TESTS_BUILD_NODES (default 2), TESTS_SLOW_SECONDS (default 5),
TESTS_OUT (default artifacts/test-runs/<timestamp>), TESTS_CACHE (default artifacts/test-runs/cache.json).

A test project can cap its chunks with <TestClassesPerChunk> in its .csproj (the command sweeps use 1: each shard class
is its own chunk even before the cache knows its time). The command sweeps are selected by --changed and --files only
through SweepRule; --sweeps forces them."""


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

    def owner(self, file):
        return max((project for project in self.projects if project.parent in file.parents),
                   key=lambda project: len(project.parent.parts), default=None)

    def chunk_size(self, project, default):
        try:
            tree = ET.parse(project)
        except ET.ParseError:
            return default
        values = [element.text for element in tree.iter()
                  if element.tag.split("}")[-1] == "TestClassesPerChunk" and element.text]
        return int(values[-1]) if values else default

    def affected_tests(self, files):
        touched = set()
        for file in files:
            if file.name in GLOBAL_FILES and file.parent == self.repo:
                return list(self.tests)
            touched |= self.linked.get(file, set())
            owner = self.owner(file)
            if owner is not None:
                touched.add(owner)
        pending = list(touched)
        while pending:
            for dependant in self.dependants.get(pending.pop(), ()):
                if dependant not in touched:
                    touched.add(dependant)
                    pending.append(dependant)
        return [project for project in self.tests if project in touched]


class SweepRule:
    MODULE_PROJECTS = {"PulseOffice.Sheets.UI": "Sheets", "PulseOffice.Documents.UI": "Documents",
                       "PulseOffice.Presentations.UI": "Presentations"}
    SHARED_PROJECTS = {"PulseOffice.Shell", "PulseOffice.Desktop", "PulseOffice.UI", "PulseOffice.UI.Commands",
                       "PulseOffice.UI.Controls", "PulseOffice.UI.Embedding", "PulseOffice.UI.Icons",
                       "PulseOffice.Document.Commands", "PulseOffice.Agent.UI", "PulseOffice.Data.UI",
                       "PulseOffice.Remote.UI", "PulseOffice.Scripting.Editor", "PulseOffice.Formulas.Editor",
                       "PulseOffice.Localization"}
    COMMAND_FILE = re.compile(r"(Command|Menu|Ribbon|Toolbar|Shortcut)", re.IGNORECASE)

    def __init__(self, repo, graph):
        self.repo = repo
        self.graph = graph

    @staticmethod
    def is_sweep(project):
        return ".Sweep." in project.stem

    @staticmethod
    def module_of(project):
        return project.stem.split(".Sweep.")[1].split(".")[0]

    def harness_files(self):
        return {file for file, owners in self.graph.linked.items() if any(self.is_sweep(owner) for owner in owners)}

    def modules_for(self, files):
        modules, reasons = set(), []
        everything = {"Sheets", "Documents", "Presentations"}
        harness = self.harness_files()
        for file in files:
            relative = file.relative_to(self.repo) if file.is_relative_to(self.repo) else file
            if file.name in GLOBAL_FILES and file.parent == self.repo:
                modules |= everything
                reasons.append(f"{relative}: global build file")
                continue
            owner = self.graph.owner(file)
            if file in harness:
                modules |= everything
                reasons.append(f"{relative}: sweep harness")
            elif owner is None:
                continue
            elif self.is_sweep(owner):
                modules.add(self.module_of(owner))
                reasons.append(f"{relative}: sweep project")
            elif owner.stem in self.MODULE_PROJECTS:
                modules.add(self.MODULE_PROJECTS[owner.stem])
                reasons.append(f"{relative}: {owner.stem} (module UI)")
            elif owner.stem in self.SHARED_PROJECTS:
                modules |= everything
                reasons.append(f"{relative}: {owner.stem} (shell, commands or shared UI)")
            elif owner.parent.parent.name == "src" and self.COMMAND_FILE.search(file.stem):
                modules |= everything
                reasons.append(f"{relative}: command, menu or ribbon file in {owner.stem}")
        return modules, reasons

    def filter(self, projects, files, forced):
        sweeps = [project for project in projects if self.is_sweep(project)]
        if forced or not sweeps:
            return projects
        modules, reasons = self.modules_for(files)
        kept = [project for project in sweeps if self.module_of(project) in modules]
        skipped = [project for project in sweeps if project not in kept]
        for reason in reasons[:10]:
            print(f"sweeps selected by {reason}")
        if skipped:
            print("sweeps skipped (no change to commands, menus, ribbon, module UI, Shell, Desktop or the sweep "
                  "harness; force with --sweeps): " + ", ".join(project.stem for project in skipped))
        return [project for project in projects if not self.is_sweep(project) or project in kept]


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
    def __init__(self, limit_mb):
        self.limit_mb = limit_mb
        self.available = sys.platform.startswith("linux") and shutil.which("systemd-run") is not None

    def unit(self):
        return f"pulse-tests-{os.getpid()}-{random.randrange(1 << 30)}"

    def wrap(self, command, unit=None, limit_mb=None):
        if not self.available:
            return command
        return ["systemd-run", "--user", "--scope", "--quiet", "--collect", "-p", f"MemoryMax={limit_mb or self.limit_mb}M",
                "-p", "MemorySwapMax=0", f"--unit={unit or self.unit()}", "--"] + command

    def stop(self, process, unit):
        if self.available:
            subprocess.run(["systemctl", "--user", "kill", "--signal=SIGKILL", f"{unit}.scope"], capture_output=True)
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except (ProcessLookupError, PermissionError, AttributeError):
            process.kill()
        process.wait()

    def peak_probe(self, command):
        if not self.available:
            return command
        script = ('"$@"; code=$?; echo "PEAK=$(cat /sys/fs/cgroup$(cut -d: -f3 /proc/self/cgroup)/memory.peak)"; '
                  'exit $code')
        return ["bash", "-c", script, "probe"] + command


class TestAssembly:
    def __init__(self, project):
        self.project = project
        self.name = project.stem
        self.folder = project.parent / "bin" / CONFIGURATION / FRAMEWORK
        self.dll = self.folder / f"{self.name}.dll"

    def command(self):
        return ["dotnet", "exec", str(self.dll)]

    def fingerprint(self):
        digest = hashlib.sha256()
        for path in sorted(self.folder.rglob("*")):
            if path.is_file():
                status = path.stat()
                digest.update(f"{path.relative_to(self.folder)}\t{status.st_size}\t{status.st_mtime_ns}\n".encode())
        return digest.hexdigest()


class MemorySize:
    UNITS = {"": 1 / 1048576, "K": 1 / 1024, "M": 1, "G": 1024, "T": 1048576}

    @staticmethod
    def megabytes(text):
        match = re.fullmatch(r"\s*(\d+)\s*([KMGT]?)\s*", text.upper())
        if not match:
            sys.exit(f"TESTS_MEMORY_MAX={text}: expected a size such as 3G or 2500M")
        return max(1, int(int(match.group(1)) * MemorySize.UNITS[match.group(2)]))


class MemoryBudget:
    def __init__(self, total_mb):
        self.total_mb = total_mb
        self.used_mb = 0
        self.condition = threading.Condition()

    def acquire(self, mb):
        with self.condition:
            self.condition.wait_for(lambda: self.used_mb == 0 or self.used_mb + mb <= self.total_mb)
            self.used_mb += mb

    def release(self, mb):
        with self.condition:
            self.used_mb -= mb
            self.condition.notify_all()


class ChunkMemory:
    MARGIN = 1.5
    STEP_MB = 256
    FLOOR_MB = 1024
    CEILING_MB = 6144

    def __init__(self, cache, unknown_mb, fixed_mb=None):
        self.cache = cache
        self.unknown_mb = unknown_mb
        self.fixed_mb = fixed_mb

    def limit_mb(self, name):
        if self.fixed_mb:
            return self.fixed_mb
        peak = self.cache.peak_mb(name)
        if not peak:
            return self.unknown_mb
        wanted = math.ceil(peak * self.MARGIN / self.STEP_MB) * self.STEP_MB
        return min(self.CEILING_MB, max(self.FLOOR_MB, wanted))


class ResultCache:
    def __init__(self, path):
        self.path = Path(path)
        self.lock = threading.Lock()
        try:
            self.entries = json.loads(self.path.read_text())
        except (OSError, ValueError):
            self.entries = {}

    def green(self, name, fingerprint):
        return self.entries.get(name, {}).get("green") == fingerprint

    def class_seconds(self, name):
        return self.entries.get(name, {}).get("classes", {})

    def record(self, name, seconds_by_class):
        with self.lock:
            self.entries.setdefault(name, {}).setdefault("classes", {}).update(seconds_by_class)

    def peak_mb(self, name):
        return self.entries.get(name, {}).get("peak_mb", 0)

    def record_peak(self, name, peak_mb):
        with self.lock:
            entry = self.entries.setdefault(name, {})
            entry["peak_mb"] = max(entry.get("peak_mb", 0), peak_mb)

    def mark_green(self, name, fingerprint):
        with self.lock:
            self.entries.setdefault(name, {})["green"] = fingerprint

    def save(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(json.dumps(self.entries, indent=1, sort_keys=True))


class Chunk:
    def __init__(self, assembly, index, classes, estimate):
        self.assembly = assembly
        self.index = index
        self.classes = classes
        self.estimate = estimate


class TestRunner:
    def __init__(self, options, cap, cache, graph, memory, budget):
        self.memory = memory
        self.budget = budget
        self.graph = graph
        self.options = options
        self.cap = cap
        self.cache = cache
        self.out = Path(options.out)
        self.summary = self.out / "summary.tsv"
        self.slow = self.out / "slow.tsv"
        self.write_lock = threading.Lock()

    def environment(self):
        env = dict(os.environ)
        env.update({"DOTNET_gcServer": "0", "DOTNET_GCConserveMemory": "7", "DOTNET_TieredPGO": "0",
                    "MSBUILDDISABLENODEREUSE": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "NO_COLOR": "1"})
        return env

    def build(self, projects, everything):
        log = self.out / "build.log"
        started = time.monotonic()
        target = REPO / "PulseOffice.slnx" if everything else self._filter(projects)
        print(f"building {'the solution' if everything else f'{len(projects)} test project(s)'} "
              f"with {self.options.nodes} nodes", flush=True)
        command = ["dotnet", "build", str(target), "-nologo", "-v", "q", f"-m:{self.options.nodes}",
                   "-nodeReuse:false", "-p:UseSharedCompilation=false"]
        with log.open("w") as stream:
            code = subprocess.run(self.cap.wrap(command), stdout=stream, stderr=subprocess.STDOUT,
                                  env=self.environment()).returncode
        subprocess.run(["dotnet", "build-server", "shutdown"], capture_output=True, env=self.environment())
        print("".join(log.read_text(errors="replace").splitlines(keepends=True)[-3:]), end="")
        print(f"build: {int(time.monotonic() - started)}s", flush=True)
        if code != 0:
            sys.exit(f"build failed, see {log}")

    def _filter(self, projects):
        path = self.out / "selection.slnf"
        path.write_text(json.dumps({"solution": {"path": str(REPO / "PulseOffice.slnx"), "projects": [
            str(project.relative_to(REPO)).replace("\\", "/") for project in projects]}}, indent=1))
        return path

    def classes(self, assembly):
        result = subprocess.run(assembly.command() + ["-list", "classes"], capture_output=True, text=True,
                                cwd=assembly.folder, env=self.environment())
        if result.returncode != 0:
            sys.exit(f"cannot list the tests of {assembly.name}:\n{result.stdout}{result.stderr}")
        return sorted(line.strip() for line in result.stdout.splitlines() if re.fullmatch(r"[\w.+`]+", line.strip()))

    def chunks(self, assembly):
        classes = self.classes(assembly)
        known = self.cache.class_seconds(assembly.name)
        estimate = {name: known.get(name, 1.0) for name in classes}
        count = max(math.ceil(sum(estimate.values()) / self.options.chunk_seconds),
                    math.ceil(len(classes) / self.graph.chunk_size(assembly.project, self.options.classes_per_chunk)), 1)
        if count == 1:
            return [Chunk(assembly, 1, [], sum(estimate.values()))]
        bins = [[] for _ in range(count)]
        loads = [0.0] * count
        for name in sorted(classes, key=lambda name: -estimate[name]):
            lightest = min(range(count), key=lambda index: (loads[index], len(bins[index])))
            bins[lightest].append(name)
            loads[lightest] += estimate[name]
        return [Chunk(assembly, index, sorted(names), load)
                for index, (names, load) in enumerate(zip(bins, loads), 1) if names]

    def run_chunk(self, chunk):
        assembly, index, classes = chunk.assembly, chunk.index, chunk.classes
        log = self.out / f"{assembly.name}.{index}.log"
        report = self.out / f"{assembly.name}.{index}.xml"
        command = assembly.command() + ["-noLogo", "-noColor", "-longRunning", str(self.options.report_seconds),
                                         "-xml", str(report)]
        for name in classes:
            command += ["-class", name]
        unit = self.cap.unit()
        limit_mb = self.memory.limit_mb(assembly.name)
        self.budget.acquire(limit_mb)
        started = time.monotonic()
        try:
            return self._run_capped(chunk, command, log, report, unit, limit_mb, started)
        finally:
            self.budget.release(limit_mb)

    def _run_capped(self, chunk, command, log, report, unit, limit_mb, started):
        assembly, index = chunk.assembly, chunk.index
        stopped = None
        with log.open("w") as stream:
            process = subprocess.Popen(self.cap.wrap(self.cap.peak_probe(command), unit, limit_mb), stdout=stream,
                                       stderr=subprocess.STDOUT, cwd=assembly.folder, env=self.environment(),
                                       start_new_session=True)
            while process.poll() is None:
                time.sleep(1)
                stopped = self._overrun(log, time.monotonic() - started)
                if stopped:
                    self.cap.stop(process, unit)
                    break
            code = process.returncode
        seconds = int(time.monotonic() - started)
        text = log.read_text(errors="replace")
        passed, failed, slow, seconds_by_class = self._results(report)
        peak = self._last(r"PEAK=(\d+)", text)
        if stopped:
            result, reason = stopped
            failed = max(failed, 1)
            with log.open("a") as stream:
                stream.write(f"\nstopped by the runner: {reason}\n")
            slow = slow + [(reason, float(seconds))]
        elif code == 0 and failed == 0:
            result = "ok"
        elif (self.cap.available and peak is None) or re.search(r"\boom\b|Killed", text, re.IGNORECASE):
            result = "killed"
        else:
            result = "failed"
        peak_mb = int(peak or 0) // 1048576
        self.cache.record(assembly.name, seconds_by_class)
        self.cache.record_peak(assembly.name, limit_mb if result == "killed" else peak_mb)
        slowest = f"{slow[0][0]} ({slow[0][1]:.1f}s)" if slow else ""
        with self.write_lock:
            with self.summary.open("a") as stream:
                stream.write(f"{assembly.name}\t{index}\t{result}\t{passed}\t{failed}\t{peak_mb}\t{limit_mb}\t{seconds}\t{slowest}\n")
            with self.slow.open("a") as stream:
                for name, duration in slow:
                    stream.write(f"{assembly.name}\t{duration:.1f}\t{name}\n")
            print(f"{assembly.name:<48} {index:>3} {result:<7} pass {passed:<5} fail {failed:<4} "
                  f"peak {peak_mb:>5}/{limit_mb} MB  {seconds:>4}s", flush=True)
            for name, duration in slow:
                print(f"    slow {duration:6.1f}s  {name}", flush=True)
        return result

    def _overrun(self, log, elapsed):
        if elapsed > self.options.chunk_timeout:
            return "timeout", f"chunk over {self.options.chunk_timeout:g}s"
        try:
            text = log.read_text(errors="replace")
        except OSError:
            return None
        for name, hours, minutes, seconds in re.findall(
                r"\[Long Running Test\] '([^']+)', Elapsed: (\d+):(\d+):(\d+)", text):
            if int(hours) * 3600 + int(minutes) * 60 + int(seconds) >= self.options.hang_seconds:
                return "hang", f"{name} running over {self.options.hang_seconds:g}s"
        return None

    def _results(self, report):
        try:
            root = ET.parse(report).getroot()
        except (OSError, ET.ParseError):
            return 0, 0, [], {}
        passed = failed = 0
        slow = []
        seconds_by_class = {}
        for test in root.iter("test"):
            owner = test.get("type")
            if owner:
                seconds_by_class[owner] = round(seconds_by_class.get(owner, 0.0) + float(test.get("time") or 0), 2)
            outcome = test.get("result")
            if outcome == "Pass":
                passed += 1
            elif outcome == "Fail":
                failed += 1
            duration = float(test.get("time") or 0)
            if duration >= self.options.slow_seconds:
                slow.append((test.get("name"), duration))
        for collection in root.iter("errors"):
            failed += len(list(collection))
        return passed, failed, sorted(slow, key=lambda item: -item[1]), seconds_by_class

    @staticmethod
    def _last(pattern, text):
        found = re.findall(pattern, text)
        return found[-1] if found else None

    def run(self, projects):
        self.out.mkdir(parents=True, exist_ok=True)
        self.summary.write_text("project\tchunk\tresult\tpassed\tfailed\tpeak_mb\tlimit_mb\tseconds\tslowest\n")
        self.slow.write_text("project\tseconds\ttest\n")
        started = time.monotonic()
        if not self.cap.available:
            print("warning: no memory cap on this system; chunks run without a limit")
        if self.options.build:
            self.build(projects, self.options.changed is None and not self.options.fragments)
        assemblies = [TestAssembly(project) for project in projects]
        fingerprints = {}
        pending = []
        for assembly in assemblies:
            if not assembly.dll.exists():
                sys.exit(f"{assembly.dll} is missing: build first")
            if self.options.cache and assembly.name not in UNCACHEABLE:
                fingerprints[assembly.name] = assembly.fingerprint()
                if self.cache.green(assembly.name, fingerprints[assembly.name]):
                    print(f"{assembly.name:<48}     cached  (green with the same build)", flush=True)
                    continue
            pending.append(assembly)
        chunks = [chunk for assembly in pending for chunk in self.chunks(assembly)]
        chunks.sort(key=lambda chunk: -chunk.estimate)
        print(f"{len(chunks)} chunk(s), up to {self.options.jobs} at a time within {self.budget.total_mb} MB, "
              f"each capped at 1.5 times its recorded peak", flush=True)
        with ThreadPoolExecutor(max_workers=max(1, self.options.jobs)) as pool:
            outcomes = list(pool.map(lambda chunk: (chunk.assembly.name, self.run_chunk(chunk)), chunks))
        for assembly in pending:
            if assembly.name in fingerprints and all(result == "ok" for name, result in outcomes if name == assembly.name):
                self.cache.mark_green(assembly.name, assembly.fingerprint())
        self.cache.save()
        return self.report(int(time.monotonic() - started))

    def report(self, seconds):
        rows = [line.split("\t") for line in self.summary.read_text().splitlines()[1:]]
        print(f"\nsummary: {self.summary}  ({seconds}s in all)")
        bad = [row for row in rows if row[2] != "ok"]
        for row in bad:
            print(f"  {row[0]} chunk {row[1]}: {row[2]}")
        if rows:
            top = max(rows, key=lambda row: int(row[5]))
            print(f"highest peak: {top[5]} MB ({top[0]} chunk {top[1]})")
        slow = len(self.slow.read_text().splitlines()) - 1
        if slow:
            print(f"{slow} test(s) over {self.options.slow_seconds:g}s: {self.slow}")
        return 1 if bad else 0


def main():
    parser = argparse.ArgumentParser(description=DESCRIPTION, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("fragments", nargs="*", help="run only test projects whose name contains one of these")
    parser.add_argument("--no-build", dest="build", action="store_false", help="use the existing build")
    parser.add_argument("--no-cache", dest="cache", action="store_false",
                        help="run every selected project even if it was green with the same build")
    parser.add_argument("--changed", nargs="?", const="main", metavar="BASE",
                        help="run only the test projects affected by files changed since BASE (default main), "
                             "including uncommitted ones; builds only those projects")
    parser.add_argument("--files", nargs="+", metavar="FILE", help="print the test projects affected by these files and exit")
    parser.add_argument("--sweeps", action="store_true",
                        help="with --changed or --files, keep the command sweeps even when SweepRule would skip them")
    parser.add_argument("--list", action="store_true", help="print the selected test projects and exit")
    options = parser.parse_args()
    runs = REPO / "artifacts" / "test-runs"
    options.out = os.environ.get("TESTS_OUT") or str(runs / datetime.now().strftime("%Y%m%d-%H%M%S"))
    options.nodes = int(os.environ.get("TESTS_BUILD_NODES", "2"))
    options.jobs = max(1, int(os.environ.get("TESTS_JOBS", "5")))
    options.chunk_seconds = float(os.environ.get("TESTS_CHUNK_SECONDS", "60"))
    total = int(os.environ.get("TESTS_MEMORY_TOTAL_GB", "12"))
    options.classes_per_chunk = int(os.environ.get("TESTS_CLASSES_PER_CHUNK", "40"))
    options.slow_seconds = float(os.environ.get("TESTS_SLOW_SECONDS", "5"))
    options.chunk_timeout = float(os.environ.get("TESTS_CHUNK_TIMEOUT", "600"))
    options.hang_seconds = float(os.environ.get("TESTS_HANG_SECONDS", "60"))
    options.report_seconds = max(1, int(options.hang_seconds // 3))

    graph = ProjectGraph(REPO)
    sweeps = SweepRule(REPO, graph)
    if options.files:
        files = [Path(name).resolve() for name in options.files]
        for project in sweeps.filter(graph.affected_tests(files), files, options.sweeps):
            print(project.stem)
        return 0
    if options.changed is not None:
        files = ChangedFiles(REPO).since(options.changed)
        projects = sweeps.filter(graph.affected_tests(files), files, options.sweeps)
    else:
        projects = graph.tests
    if options.fragments:
        projects = [project for project in projects if any(fragment in project.stem for fragment in options.fragments)]
    if options.list:
        for project in projects:
            print(project.stem)
        return 0
    if not projects:
        print("no test project selected")
        return 0
    cache = ResultCache(os.environ.get("TESTS_CACHE") or runs / "cache.json")
    unknown_mb = min(ChunkMemory.CEILING_MB, total * 1024 // 3)
    fixed = os.environ.get("TESTS_MEMORY_MAX")
    fixed_mb = MemorySize.megabytes(fixed) if fixed else None
    memory = ChunkMemory(cache, unknown_mb, fixed_mb)
    return TestRunner(options, MemoryCap(fixed_mb or unknown_mb), cache, graph, memory, MemoryBudget(total * 1024)).run(projects)


if __name__ == "__main__":
    sys.exit(main())
