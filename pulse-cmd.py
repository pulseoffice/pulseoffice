#!/usr/bin/env python3
import argparse
import getpass
import os
import plistlib
import re
import shutil
import subprocess
import sys
import textwrap
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROPS = ROOT / "Directory.Build.props"
MANIFEST = ROOT / "src" / "PulseOffice.Desktop" / "app.manifest"
DESKTOP_PROJECT = ROOT / "src" / "PulseOffice.Desktop" / "PulseOffice.Desktop.csproj"
ARTIFACTS = ROOT / "artifacts"
PACKAGING = ROOT / "packaging"

APP_NAME = "PulseOffice"
APP_ID = "io.pulseoffice.PulseOffice"
EXECUTABLE = "PulseOffice"
PUBLISHER = "PulseOffice"
LICENSE_ID = "GPL-3.0-only"
LICENSE_FILE = ROOT / "LICENSE"


class Console:
    def step(self, message):
        print(f"\n\033[1;34m==>\033[0m {message}", flush=True)

    def info(self, message):
        print(f"    {message}", flush=True)

    def fail(self, message):
        print(f"\n\033[1;31mError:\033[0m {message}", file=sys.stderr)
        sys.exit(1)

    automatic = False

    def ask(self, prompt, default=None):
        if self.automatic and default:
            self.info(f"{prompt}: {default}")
            return default
        suffix = f" [{default}]" if default else ""
        answer = input(f"{prompt}{suffix}: ").strip()
        return answer or (default or "")

    def secret(self, prompt):
        return getpass.getpass(f"{prompt}: ")

    def confirm(self, prompt, default=True):
        if self.automatic:
            self.info(f"{prompt} yes")
            return True
        hint = "Y/n" if default else "y/N"
        answer = input(f"{prompt} [{hint}]: ").strip().lower()
        if not answer:
            return default
        return answer in ("y", "yes")

    def choose(self, prompt, options):
        for index, option in enumerate(options, start=1):
            print(f"  {index}) {option}")
        while True:
            answer = input(f"{prompt} [1-{len(options)}]: ").strip()
            if answer.isdigit() and 1 <= int(answer) <= len(options):
                return int(answer) - 1


class Shell:
    def __init__(self, console):
        self.console = console

    def run(self, command, cwd=None, capture=False, secret_args=()):
        shown = " ".join("••••" if part in secret_args else str(part) for part in command)
        self.console.info(f"$ {shown}")
        result = subprocess.run(
            [str(part) for part in command],
            cwd=str(cwd) if cwd else None,
            text=True,
            capture_output=capture,
        )
        if result.returncode != 0:
            if capture:
                sys.stderr.write(result.stdout or "")
                sys.stderr.write(result.stderr or "")
            self.console.fail(f"command exited with code {result.returncode}")
        return result.stdout if capture else ""

    def succeeds(self, command, cwd=None):
        result = subprocess.run([str(part) for part in command], cwd=str(cwd) if cwd else None,
                                text=True, capture_output=True)
        return result.returncode == 0

    def require(self, tool):
        if shutil.which(tool) is None:
            self.console.fail(f"'{tool}' is not installed or not on PATH")


class ProjectVersion:
    PATTERN = re.compile(r"<Version>([^<]+)</Version>")

    def __init__(self, console):
        self.console = console

    def current(self):
        match = self.PATTERN.search(PROPS.read_text(encoding="utf-8"))
        return match.group(1).strip() if match else "0.1.0"

    def bumped(self, part):
        major, minor, patch = (int(piece) for piece in self.current().split("-")[0].split(".")[:3])
        if part == "major":
            return f"{major + 1}.0.0"
        if part == "minor":
            return f"{major}.{minor + 1}.0"
        return f"{major}.{minor}.{patch + 1}"

    def write(self, version):
        if not re.fullmatch(r"\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?", version):
            self.console.fail(f"invalid version: {version}")
        text = PROPS.read_text(encoding="utf-8")
        if self.PATTERN.search(text):
            text = self.PATTERN.sub(f"<Version>{version}</Version>", text, count=1)
        else:
            text = text.replace("<Product>", f"<Version>{version}</Version>\n    <Product>", 1)
        PROPS.write_text(text, encoding="utf-8")
        numeric = version.split("-")[0]
        manifest = MANIFEST.read_text(encoding="utf-8")
        manifest = re.sub(r'(<assemblyIdentity version=")[^"]+(")', rf"\g<1>{numeric}.0\g<2>", manifest, count=1)
        MANIFEST.write_text(manifest, encoding="utf-8")


class Publisher:
    def __init__(self, shell, console):
        self.shell = shell
        self.console = console

    def publish(self, runtime, version):
        output = ARTIFACTS / "publish" / runtime
        if output.exists():
            shutil.rmtree(output)
        self.console.step(f"Publishing {runtime}")
        self.shell.run([
            "dotnet", "publish", DESKTOP_PROJECT,
            "-c", "Release",
            "-r", runtime,
            "--self-contained", "true",
            f"-p:Version={version}",
            "-p:DebugType=None",
            "-p:DebugSymbols=false",
            "-o", output,
        ])
        return output


class MacPackager:
    ENTITLEMENTS = {
        "com.apple.security.cs.allow-jit": True,
        "com.apple.security.cs.allow-unsigned-executable-memory": True,
        "com.apple.security.cs.disable-library-validation": True,
        "com.apple.security.cs.allow-dyld-environment-variables": True,
    }

    def __init__(self, shell, console, publisher):
        self.shell = shell
        self.console = console
        self.publisher = publisher

    def identities(self):
        listing = self.shell.run(["security", "find-identity", "-v", "-p", "codesigning"], capture=True)
        return re.findall(r'"(Developer ID Application: [^"]+)"', listing)

    def choose_identity(self):
        found = self.identities()
        if not found:
            self.console.fail("no 'Developer ID Application' certificate found in the keychain")
        if len(found) == 1:
            return found[0]
        return found[self.console.choose("Signing certificate", found)]

    def build(self, version, architectures, notarize):
        self.shell.require("codesign")
        self.shell.require("hdiutil")
        identity = self.choose_identity()
        profile = self.notary_profile() if notarize else None
        entitlements = ARTIFACTS / "mac" / "entitlements.plist"
        entitlements.parent.mkdir(parents=True, exist_ok=True)
        with entitlements.open("wb") as handle:
            plistlib.dump(self.ENTITLEMENTS, handle)
        for architecture in architectures:
            runtime = "osx-arm64" if architecture == "arm64" else "osx-x64"
            published = self.publisher.publish(runtime, version)
            bundle = self.bundle(published, version, architecture)
            self.sign_bundle(bundle, identity, entitlements)
            dmg = self.disk_image(bundle, version, architecture, identity)
            if profile:
                self.notarize(dmg, profile)
            self.console.step(f"Ready: {dmg}")

    def bundle(self, published, version, architecture):
        self.console.step(f"Building .app bundle ({architecture})")
        staging = ARTIFACTS / "mac" / architecture
        if staging.exists():
            shutil.rmtree(staging)
        bundle = staging / f"{APP_NAME}.app"
        macos = bundle / "Contents" / "MacOS"
        resources = bundle / "Contents" / "Resources"
        shutil.copytree(published, macos)
        shutil.copy(LICENSE_FILE, macos / "LICENSE")
        resources.mkdir(parents=True)
        icon = self.icon(resources)
        info = {
            "CFBundleName": APP_NAME,
            "CFBundleDisplayName": APP_NAME,
            "CFBundleIdentifier": APP_ID,
            "CFBundleExecutable": EXECUTABLE,
            "CFBundlePackageType": "APPL",
            "CFBundleShortVersionString": version.split("-")[0],
            "CFBundleVersion": version.split("-")[0],
            "CFBundleInfoDictionaryVersion": "6.0",
            "LSMinimumSystemVersion": "12.0",
            "NSHighResolutionCapable": True,
            "NSPrincipalClass": "NSApplication",
            "LSApplicationCategoryType": "public.app-category.productivity",
            "NSHumanReadableCopyright": f"{PUBLISHER} — {LICENSE_ID}",
        }
        if icon:
            info["CFBundleIconFile"] = icon
        with (bundle / "Contents" / "Info.plist").open("wb") as handle:
            plistlib.dump(info, handle)
        (macos / EXECUTABLE).chmod(0o755)
        return bundle

    def icon(self, resources):
        source = PACKAGING / "icon.png"
        if not source.exists():
            self.console.info("packaging/icon.png not found: the bundle will use the generic icon")
            return None
        iconset = ARTIFACTS / "mac" / f"{APP_NAME}.iconset"
        if iconset.exists():
            shutil.rmtree(iconset)
        iconset.mkdir(parents=True)
        for size in (16, 32, 128, 256, 512):
            for scale in (1, 2):
                name = f"icon_{size}x{size}{'@2x' if scale == 2 else ''}.png"
                pixels = size * scale
                self.shell.run(["sips", "-z", pixels, pixels, source, "--out", iconset / name], capture=True)
        self.shell.run(["iconutil", "-c", "icns", iconset, "-o", resources / f"{APP_NAME}.icns"])
        return f"{APP_NAME}.icns"

    def sign_bundle(self, bundle, identity, entitlements):
        self.console.step("Code signing")
        macos = bundle / "Contents" / "MacOS"
        files = sorted(path for path in macos.rglob("*") if path.is_file() and path.name != EXECUTABLE)
        for path in files:
            self.codesign(path, identity, entitlements)
        self.codesign(macos / EXECUTABLE, identity, entitlements)
        self.codesign(bundle, identity, entitlements)
        self.shell.run(["codesign", "--verify", "--strict", "--verbose=2", bundle])

    def codesign(self, path, identity, entitlements):
        subprocess.run(
            ["codesign", "--force", "--timestamp", "--options", "runtime",
             "--entitlements", str(entitlements), "--sign", identity, str(path)],
            check=True,
            capture_output=True,
        )

    def disk_image(self, bundle, version, architecture, identity):
        self.console.step(f"Disk image ({architecture})")
        content = bundle.parent / "dmg"
        if content.exists():
            shutil.rmtree(content)
        content.mkdir()
        self.shell.run(["ditto", bundle, content / bundle.name])
        (content / "Applications").symlink_to("/Applications")
        background = content / ".background"
        background.mkdir()
        self.background(background / "background.tiff")
        volume = f"{APP_NAME} {version}"
        writable = bundle.parent / f"{APP_NAME}-{architecture}-rw.dmg"
        if writable.exists():
            writable.unlink()
        self.shell.run(["hdiutil", "create", "-volname", volume, "-srcfolder", content, "-fs", "HFS+",
                        "-format", "UDRW", "-ov", writable])
        mount = Path("/Volumes") / volume
        if mount.exists():
            self.shell.run(["hdiutil", "detach", mount, "-force"])
        self.shell.run(["hdiutil", "attach", writable, "-readwrite", "-noverify", "-noautoopen"])
        try:
            self.arrange(mount, volume, bundle.name)
        finally:
            self.shell.run(["hdiutil", "detach", mount])
        dmg = ARTIFACTS / f"{APP_NAME}-{version}-macos-{architecture}.dmg"
        if dmg.exists():
            dmg.unlink()
        self.shell.run(["hdiutil", "convert", writable, "-format", "UDZO", "-imagekey", "zlib-level=9", "-o", dmg])
        writable.unlink()
        self.shell.run(["codesign", "--force", "--timestamp", "--sign", identity, dmg])
        return dmg

    def background(self, target):
        retina = PACKAGING / "dmg-background.png"
        standard = target.parent / "background.png"
        self.shell.run(["sips", "-z", "400", "660", retina, "--out", standard], capture=True)
        self.shell.run(["tiffutil", "-cathidpicheck", standard, retina, "-out", target], capture=True)
        standard.unlink()

    def arrange(self, mount, volume, application):
        self.console.info("Arranging the disk image window (macOS may ask to let the terminal control Finder)")
        script = textwrap.dedent(f'''
            tell application "Finder"
                tell disk "{volume}"
                    open
                    set current view of container window to icon view
                    set toolbar visible of container window to false
                    set statusbar visible of container window to false
                    set bounds of container window to {{200, 120, 860, 548}}
                    set options to the icon view options of container window
                    set arrangement of options to not arranged
                    set icon size of options to 128
                    set text size of options to 13
                    set background picture of options to file ".background:background.tiff"
                    set position of item "{application}" of container window to {{165, 190}}
                    set position of item "Applications" of container window to {{495, 190}}
                    update without registering applications
                    delay 1
                    close
                end tell
            end tell
        ''')
        self.shell.run(["osascript", "-e", script])
        layout = mount / ".DS_Store"
        arranged = layout.read_bytes()
        time.sleep(6)
        layout.write_bytes(arranged)
        self.shell.run(["sync"])

    def notary_profile(self):
        self.console.step("Notarization credentials")
        self.console.info("Credentials are stored in the macOS keychain, never in this repository.")
        name = self.console.ask("notarytool keychain profile name", "pulseoffice-notary")
        if self.console.confirm("Does the profile already exist?", True):
            return name
        apple_id = self.console.ask("Apple ID (email)")
        team_id = self.console.ask("Team ID (10 characters)")
        password = self.console.secret("App-specific password (appleid.apple.com)")
        self.shell.run(
            ["xcrun", "notarytool", "store-credentials", name,
             "--apple-id", apple_id, "--team-id", team_id, "--password", password],
            secret_args=(password,),
        )
        return name

    def notarize(self, dmg, profile):
        self.console.step(f"Notarizing {dmg.name} (this may take a few minutes)")
        self.shell.run(["xcrun", "notarytool", "submit", dmg, "--keychain-profile", profile, "--wait"])
        self.shell.run(["xcrun", "stapler", "staple", dmg])
        self.shell.run(["spctl", "--assess", "--type", "open", "--context", "context:primary-signature", "-v", dmg])


class WindowsPackager:
    def __init__(self, shell, console, publisher):
        self.shell = shell
        self.console = console
        self.publisher = publisher

    def build(self, version, architectures):
        self.shell.require("makensis")
        for architecture in architectures:
            runtime = f"win-{architecture}"
            published = self.publisher.publish(runtime, version)
            script = self.script(published, version, architecture)
            self.console.step(f"NSIS installer ({architecture})")
            self.shell.run(["makensis", "-V2", "-INPUTCHARSET", "UTF8", script])
            self.console.step(f"Ready: {ARTIFACTS / self.installer_name(version, architecture)}")

    def installer_name(self, version, architecture):
        return f"{APP_NAME}-{version}-windows-{architecture}-setup.exe"

    def script(self, published, version, architecture):
        numeric = version.split("-")[0]
        icon = PACKAGING / "icon.ico"
        icon_lines = f'!define MUI_ICON "{icon}"\n!define MUI_UNICON "{icon}"' if icon.exists() else ""
        registry = f"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{APP_NAME}"
        text = textwrap.dedent(f"""\
            Unicode true
            ManifestDPIAware true
            !include "MUI2.nsh"
            !include "x64.nsh"
            {icon_lines}
            Name "{APP_NAME} {version}"
            OutFile "{ARTIFACTS / self.installer_name(version, architecture)}"
            InstallDir "$PROGRAMFILES64\\{APP_NAME}"
            InstallDirRegKey HKLM "Software\\{APP_NAME}" "InstallDir"
            RequestExecutionLevel admin
            !define MUI_LANGDLL_REGISTRY_ROOT HKLM
            !define MUI_LANGDLL_REGISTRY_KEY "Software\\{APP_NAME}"
            !define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"
            !define MUI_LANGDLL_ALLLANGUAGES
            SetCompressor /SOLID lzma
            VIProductVersion "{numeric}.0"
            VIAddVersionKey "ProductName" "{APP_NAME}"
            VIAddVersionKey "ProductVersion" "{version}"
            VIAddVersionKey "FileVersion" "{numeric}"
            VIAddVersionKey "CompanyName" "{PUBLISHER}"
            VIAddVersionKey "FileDescription" "{APP_NAME} Setup"
            VIAddVersionKey "LegalCopyright" "{PUBLISHER}"
            !insertmacro MUI_PAGE_LICENSE "{LICENSE_FILE}"
            !insertmacro MUI_PAGE_DIRECTORY
            !insertmacro MUI_PAGE_INSTFILES
            !insertmacro MUI_PAGE_FINISH
            !insertmacro MUI_UNPAGE_CONFIRM
            !insertmacro MUI_UNPAGE_INSTFILES
            !insertmacro MUI_LANGUAGE "Italian"
            !insertmacro MUI_LANGUAGE "English"
            !insertmacro MUI_LANGUAGE "German"
            !insertmacro MUI_LANGUAGE "Spanish"
            !insertmacro MUI_LANGUAGE "French"
            !insertmacro MUI_RESERVEFILE_LANGDLL
            LangString Requires64 ${{LANG_ITALIAN}} "{APP_NAME} richiede Windows a 64 bit."
            LangString Requires64 ${{LANG_ENGLISH}} "{APP_NAME} requires 64-bit Windows."
            LangString Requires64 ${{LANG_GERMAN}} "{APP_NAME} erfordert ein 64-Bit-Windows."
            LangString Requires64 ${{LANG_SPANISH}} "{APP_NAME} requiere Windows de 64 bits."
            LangString Requires64 ${{LANG_FRENCH}} "{APP_NAME} nécessite Windows 64 bits."
            Function .onInit
              !insertmacro MUI_LANGDLL_DISPLAY
              ${{IfNot}} ${{RunningX64}}
                MessageBox MB_ICONSTOP "$(Requires64)"
                Abort
              ${{EndIf}}
              SetRegView 64
            FunctionEnd
            Section "Install"
              SetOutPath "$INSTDIR"
              File /r "{published}{os.sep}*"
              File "/oname=LICENSE.txt" "{LICENSE_FILE}"
              WriteUninstaller "$INSTDIR\\Uninstall.exe"
              CreateDirectory "$SMPROGRAMS\\{APP_NAME}"
              CreateShortcut "$SMPROGRAMS\\{APP_NAME}\\{APP_NAME}.lnk" "$INSTDIR\\{EXECUTABLE}.exe"
              CreateShortcut "$DESKTOP\\{APP_NAME}.lnk" "$INSTDIR\\{EXECUTABLE}.exe"
              WriteRegStr HKLM "Software\\{APP_NAME}" "InstallDir" "$INSTDIR"
              WriteRegStr HKLM "{registry}" "DisplayName" "{APP_NAME}"
              WriteRegStr HKLM "{registry}" "DisplayVersion" "{version}"
              WriteRegStr HKLM "{registry}" "Publisher" "{PUBLISHER}"
              WriteRegStr HKLM "{registry}" "DisplayIcon" "$INSTDIR\\{EXECUTABLE}.exe"
              WriteRegStr HKLM "{registry}" "UninstallString" '"$INSTDIR\\Uninstall.exe"'
              WriteRegDWORD HKLM "{registry}" "NoModify" 1
              WriteRegDWORD HKLM "{registry}" "NoRepair" 1
            SectionEnd
            Function un.onInit
              !insertmacro MUI_UNGETLANGUAGE
              SetRegView 64
            FunctionEnd
            Section "Uninstall"
              Delete "$DESKTOP\\{APP_NAME}.lnk"
              RMDir /r "$SMPROGRAMS\\{APP_NAME}"
              RMDir /r "$INSTDIR"
              DeleteRegKey HKLM "{registry}"
              DeleteRegKey HKLM "Software\\{APP_NAME}"
            SectionEnd
            """)
        path = ARTIFACTS / "windows" / f"installer-{architecture}.nsi"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path


class AppImagePackager:
    IMAGES = {"x86_64": "amd64/debian:bookworm-slim", "aarch64": "arm64v8/debian:bookworm-slim"}
    TOOL = "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-{arch}.AppImage"
    MIME_TYPES = (
        "application/vnd.oasis.opendocument.text;application/vnd.oasis.opendocument.spreadsheet;"
        "application/vnd.oasis.opendocument.presentation;"
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document;"
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;"
        "application/vnd.openxmlformats-officedocument.presentationml.presentation;application/rtf;text/csv;"
    )

    def __init__(self, shell, console, publisher):
        self.shell = shell
        self.console = console
        self.publisher = publisher

    def build(self, version, architectures):
        self.shell.require("docker")
        for architecture in architectures:
            runtime = "linux-x64" if architecture == "x86_64" else "linux-arm64"
            platform = "linux/amd64" if architecture == "x86_64" else "linux/arm64"
            published = self.publisher.publish(runtime, version)
            workspace = self.app_dir(published, version, architecture)
            image = f"{APP_NAME}-{version}-linux-{architecture}.AppImage"
            self.console.step(f"AppImage ({architecture}) inside Docker ({self.IMAGES[architecture]})")
            tool = self.TOOL.format(arch=architecture)
            commands = " && ".join([
                "apt-get update -qq",
                "apt-get install -y -qq --no-install-recommends ca-certificates curl file desktop-file-utils >/dev/null",
                f"curl -fsSL -o /tmp/appimagetool {tool}",
                "chmod +x /tmp/appimagetool",
                "printf '\\0\\0\\0' | dd of=/tmp/appimagetool bs=1 seek=8 conv=notrunc status=none",
                f"ARCH={architecture} APPIMAGE_EXTRACT_AND_RUN=1 /tmp/appimagetool --no-appstream /work/AppDir /out/{image}",
            ])
            self.shell.run([
                "docker", "run", "--rm", "--platform", platform,
                "-v", f"{workspace}:/work", "-v", f"{ARTIFACTS}:/out",
                self.IMAGES[architecture], "bash", "-c", commands,
            ])
            self.console.step(f"Ready: {ARTIFACTS / image}")

    def app_dir(self, published, version, architecture):
        workspace = ARTIFACTS / "appimage" / architecture
        if workspace.exists():
            shutil.rmtree(workspace)
        app_dir = workspace / "AppDir"
        lib = app_dir / "usr" / "lib" / EXECUTABLE.lower()
        shutil.copytree(published, lib)
        shutil.copy(LICENSE_FILE, lib / "LICENSE")
        (lib / EXECUTABLE).chmod(0o755)
        app_run = app_dir / "AppRun"
        app_run.write_text(textwrap.dedent(f"""\
            #!/bin/sh
            here="$(dirname "$(readlink -f "$0")")"
            exec "$here/usr/lib/{EXECUTABLE.lower()}/{EXECUTABLE}" "$@"
            """), encoding="utf-8")
        app_run.chmod(0o755)
        (app_dir / f"{APP_ID}.desktop").write_text(textwrap.dedent(f"""\
            [Desktop Entry]
            Type=Application
            Name={APP_NAME}
            GenericName=Office Suite
            Comment=Documents, spreadsheets and presentations
            Exec={EXECUTABLE} %F
            Icon={APP_ID}
            Terminal=false
            Categories=Office;
            MimeType={self.MIME_TYPES}
            StartupWMClass={APP_NAME}
            X-AppImage-Version={version}
            """), encoding="utf-8")
        icon = PACKAGING / "icon.png"
        shutil.copy(icon, app_dir / f"{APP_ID}.png")
        shutil.copy(icon, app_dir / ".DirIcon")
        self.normalize_modes(app_dir)
        return workspace

    @staticmethod
    def normalize_modes(root):
        for path in [root, *root.rglob("*")]:
            if path.is_symlink():
                continue
            executable = path.is_dir() or path.stat().st_mode & 0o111
            path.chmod(0o755 if executable else 0o644)


class Git:
    def __init__(self, shell, console):
        self.shell = shell
        self.console = console

    RELEASE_BRANCH = "main"

    def require_release_branch(self):
        branch = self.shell.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=ROOT, capture=True).strip()
        if branch != self.RELEASE_BRANCH:
            self.console.fail(
                f"releases are made from '{self.RELEASE_BRANCH}', but the current branch is '{branch}': "
                f"run 'git switch {self.RELEASE_BRANCH} && git pull' first")

    def commit_and_tag(self, version, push=None):
        self.require_release_branch()
        self.shell.run(["git", "add", PROPS, MANIFEST], cwd=ROOT)
        if not self.shell.succeeds(["git", "diff", "--cached", "--quiet"], cwd=ROOT):
            self.shell.run(["git", "commit", "-m", f"Version {version}"], cwd=ROOT)
        if not self.shell.succeeds(["git", "rev-parse", "-q", "--verify", f"refs/tags/v{version}"], cwd=ROOT):
            self.shell.run(["git", "tag", "-a", f"v{version}", "-m", f"{APP_NAME} {version}"], cwd=ROOT)
        if push if push is not None else self.console.confirm("Push the commit and the tag?", False):
            self.shell.run(["git", "push"], cwd=ROOT)
            self.shell.run(["git", "push", "origin", f"v{version}"], cwd=ROOT)


class UpdateSigning:
    PUBLIC_KEY = ROOT / "src" / "PulseOffice.Updates" / "Trust" / "update-key.pem"
    DEFAULT_PRIVATE_KEY = Path.home() / ".pulseoffice" / "update-signing-key.pem"
    CHECKSUMS = "SHA256SUMS"
    SIGNATURE = "SHA256SUMS.sig"

    def __init__(self, shell, console):
        self.shell = shell
        self.console = console

    def create_key(self):
        self.shell.require("openssl")
        self.console.step("Update signing key")
        self.console.info("The private key stays outside the repository; only the public key is committed.")
        private = Path(self.console.ask("Where to keep the private key", str(self.DEFAULT_PRIVATE_KEY))).expanduser()
        if private.exists():
            self.console.fail(f"{private} already exists: remove it first if you really want a new key")
        private.parent.mkdir(parents=True, exist_ok=True)
        self.console.info("openssl now asks for a passphrase that protects the private key.")
        self.shell.run(["openssl", "genpkey", "-algorithm", "EC", "-pkeyopt", "ec_paramgen_curve:P-256",
                        "-aes-256-cbc", "-out", private])
        os.chmod(private, 0o600)
        self.PUBLIC_KEY.parent.mkdir(parents=True, exist_ok=True)
        self.shell.run(["openssl", "pkey", "-in", private, "-pubout", "-out", self.PUBLIC_KEY])
        self.console.step(f"Public key written to {self.PUBLIC_KEY.relative_to(ROOT)}: commit it, then build the packages")

    def sign(self, packages):
        self.shell.require("openssl")
        if not self.PUBLIC_KEY.exists():
            self.console.fail("no update signing key: run 'pulse-cmd.py update-key' first")
        checksums = ARTIFACTS / self.CHECKSUMS
        signature = ARTIFACTS / self.SIGNATURE
        lines = [f"{self.digest(package)}  {package.name}\n" for package in packages]
        checksums.write_text("".join(lines), encoding="ascii")
        private = Path(self.console.ask("Private update signing key", str(self.DEFAULT_PRIVATE_KEY))).expanduser()
        if not private.exists():
            self.console.fail(f"{private} does not exist")
        self.console.info("openssl asks for the key's passphrase.")
        self.shell.run(["openssl", "dgst", "-sha256", "-sign", private, "-out", signature, checksums])
        self.shell.run(["openssl", "dgst", "-sha256", "-verify", self.PUBLIC_KEY, "-signature", signature, checksums])
        return [checksums, signature]

    @staticmethod
    def digest(path):
        import hashlib
        hasher = hashlib.sha256()
        with path.open("rb") as handle:
            for block in iter(lambda: handle.read(1 << 20), b""):
                hasher.update(block)
        return hasher.hexdigest()


class GitHubRelease:
    PATTERNS = ("{name}-{version}-macos-*.dmg", "{name}-{version}-windows-*-setup.exe", "{name}-{version}-linux-*.AppImage")

    def __init__(self, shell, console, git, signing):
        self.shell = shell
        self.console = console
        self.git = git
        self.signing = signing

    def packages(self, version):
        found = []
        for pattern in self.PATTERNS:
            found.extend(sorted(ARTIFACTS.glob(pattern.format(name=APP_NAME, version=version))))
        return found

    def publish(self, version, notes_file=None, draft=False, prerelease=False):
        self.git.require_release_branch()
        self.shell.require("gh")
        self.shell.run(["gh", "auth", "status"], cwd=ROOT, capture=True)
        tag = f"v{version}"
        packages = self.packages(version)
        if not packages:
            self.console.fail(f"no packages for version {version} in {ARTIFACTS}: build them first")
        if self.shell.succeeds(["gh", "release", "view", tag], cwd=ROOT):
            self.console.fail(f"release {tag} already exists on GitHub")
        if not self.shell.run(["git", "ls-remote", "--tags", "origin", f"refs/tags/{tag}"], cwd=ROOT, capture=True).strip():
            self.console.step(f"Tag {tag} is not on GitHub yet")
            if not self.console.confirm("Commit the version, create and push the tag now?", False):
                self.console.fail("a release needs its tag on GitHub")
            self.git.commit_and_tag(version, push=True)
        self.console.step(f"Release {APP_NAME} {version}" + (" (draft)" if draft else ""))
        packages = packages + self.signing.sign(packages)
        for package in packages:
            self.console.info(f"{package.name}  {package.stat().st_size / 1_048_576:.1f} MB")
        if not self.console.confirm("Publish this release on GitHub? It will be public", False):
            self.console.fail("release cancelled")
        command = ["gh", "release", "create", tag, *packages, "--title", f"{APP_NAME} {version}", "--verify-tag"]
        command += ["--notes-file", notes_file] if notes_file else ["--generate-notes"]
        if draft:
            command.append("--draft")
        if prerelease:
            command.append("--prerelease")
        self.shell.run(command, cwd=ROOT)
        self.console.step(f"Released {tag}")


class PulseCommand:
    def __init__(self):
        self.console = Console()
        self.shell = Shell(self.console)
        self.version = ProjectVersion(self.console)
        self.publisher = Publisher(self.shell, self.console)
        self.mac = MacPackager(self.shell, self.console, self.publisher)
        self.windows = WindowsPackager(self.shell, self.console, self.publisher)
        self.appimage = AppImagePackager(self.shell, self.console, self.publisher)
        self.git = Git(self.shell, self.console)
        self.signing = UpdateSigning(self.shell, self.console)
        self.release = GitHubRelease(self.shell, self.console, self.git, self.signing)

    def parser(self):
        parser = argparse.ArgumentParser(prog="pulse-cmd.py", description=f"{APP_NAME} versioning and packaging")
        commands = parser.add_subparsers(dest="command")
        commands.add_parser("version", help="show the current version")
        bump = commands.add_parser("bump", help="bump the version")
        bump.add_argument("part", choices=["major", "minor", "patch"], nargs="?")
        bump.add_argument("--set", dest="explicit", help="set an explicit version, e.g. 1.2.0")
        bump.add_argument("--git", action="store_true", help="commit and tag the new version")
        mac = commands.add_parser("mac", help="signed dmg images for macOS")
        mac.add_argument("--arch", choices=["arm64", "x64", "all"], default="all")
        mac.add_argument("--no-notarize", action="store_true")
        windows = commands.add_parser("windows", help="NSIS installer for Windows")
        windows.add_argument("--arch", choices=["x64", "arm64", "all"], default="x64")
        linux = commands.add_parser("linux", help="AppImage built with Docker")
        linux.add_argument("--arch", choices=["x86_64", "aarch64", "all"], default="x86_64")
        everything = commands.add_parser("all", help="all packages")
        everything.add_argument("--no-notarize", action="store_true")
        everything.add_argument("--release", action="store_true", help="publish a GitHub release afterwards")
        everything.add_argument("--force", action="store_true", help="answer yes to every confirmation and take the defaults")
        commands.add_parser("update-key", help="create the key that signs update checksums (once)")
        release = commands.add_parser("release", help="publish a GitHub release with the built packages")
        release.add_argument("--notes", dest="notes_file", help="release notes file (default: generated from commits)")
        release.add_argument("--draft", action="store_true")
        release.add_argument("--prerelease", action="store_true")
        release.add_argument("--force", action="store_true", help="answer yes to every confirmation and take the defaults")
        return parser

    def run(self, argv):
        arguments = self.parser().parse_args(argv)
        self.shell.require("dotnet")
        if arguments.command is None:
            return self.menu()
        self.console.automatic = getattr(arguments, "force", False)
        handler = getattr(self, f"command_{arguments.command.replace('-', '_')}")
        handler(arguments)

    def menu(self):
        print(f"{APP_NAME} — version {self.version.current()}")
        actions = [
            ("Bump the version", self.interactive_bump),
            ("macOS dmg (arm64 and Intel)", lambda: self.mac.build(self.version.current(), ["arm64", "x64"],
                                                                    self.console.confirm("Notarize?", True))),
            ("Windows installer (x64)", lambda: self.windows.build(self.version.current(), ["x64"])),
            ("Linux AppImage (x86_64 and aarch64)", lambda: self.appimage.build(self.version.current(), ["x86_64", "aarch64"])),
            ("All packages", lambda: self.build_all(not self.console.confirm("Notarize the dmg images?", True))),
            ("Create the update signing key", self.signing.create_key),
            ("GitHub release with the built packages", lambda: self.release.publish(
                self.version.current(), draft=self.console.confirm("Draft?", False))),
            ("Quit", lambda: None),
        ]
        index = self.console.choose("Choice", [label for label, _ in actions])
        actions[index][1]()

    def interactive_bump(self):
        current = self.version.current()
        options = [f"patch → {self.version.bumped('patch')}", f"minor → {self.version.bumped('minor')}",
                   f"major → {self.version.bumped('major')}", "explicit version"]
        index = self.console.choose(f"Current version {current}", options)
        target = self.console.ask("New version") if index == 3 else self.version.bumped(
            ["patch", "minor", "major"][index])
        self.apply_version(target, self.console.confirm("Git commit and tag?", False))

    def apply_version(self, target, tag):
        self.version.write(target)
        self.console.step(f"Version {target}")
        if tag:
            self.git.commit_and_tag(target)

    def build_all(self, skip_notarize):
        version = self.version.current()
        self.mac.build(version, ["arm64", "x64"], not skip_notarize)
        self.windows.build(version, ["x64"])
        self.appimage.build(version, ["x86_64", "aarch64"])

    def command_version(self, arguments):
        print(self.version.current())

    def command_bump(self, arguments):
        if not arguments.explicit and not arguments.part:
            return self.interactive_bump()
        self.apply_version(arguments.explicit or self.version.bumped(arguments.part), arguments.git)

    def command_mac(self, arguments):
        architectures = ["arm64", "x64"] if arguments.arch == "all" else [arguments.arch]
        self.mac.build(self.version.current(), architectures, not arguments.no_notarize)

    def command_windows(self, arguments):
        architectures = ["x64", "arm64"] if arguments.arch == "all" else [arguments.arch]
        self.windows.build(self.version.current(), architectures)

    def command_linux(self, arguments):
        architectures = ["x86_64", "aarch64"] if arguments.arch == "all" else [arguments.arch]
        self.appimage.build(self.version.current(), architectures)

    def command_all(self, arguments):
        if arguments.release:
            self.git.require_release_branch()
        self.build_all(arguments.no_notarize)
        if arguments.release:
            self.release.publish(self.version.current())

    def command_update_key(self, arguments):
        self.signing.create_key()

    def command_release(self, arguments):
        self.release.publish(self.version.current(), arguments.notes_file, arguments.draft, arguments.prerelease)


if __name__ == "__main__":
    try:
        PulseCommand().run(sys.argv[1:])
    except KeyboardInterrupt:
        print("\nInterrupted.")
        sys.exit(130)
