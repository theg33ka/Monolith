"""Hash current laboratory inputs independently of previous compiled manifests."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def capture():
    paths = set()
    for relative in (
        "Content.Server/_Forge/KIAS", "Content.Shared/_Forge/KIAS", "Content.Client/_Forge/KIAS",
        "Content.IntegrationTests/Tests/_Forge/KIAS", "Resources/Prototypes/_Forge/KIAS",
        "Resources/Locale/ru-RU/_Forge/KIAS", "Resources/Locale/en-US/_Forge/KIAS", "Tools/KiasBenchmark",
    ):
        paths.update(path for path in (ROOT / relative).rglob("*")
                     if path.is_file() and path.suffix in (".cs", ".yml", ".ftl", ".py", ".ps1", ".md", ".json"))
    paths.update((ROOT / "Run_KIAS_Tests.bat", ROOT / "Resources/Maps/_Forge/Shuttles/Archive/Mercenary/briarKIAS.yml"))
    files = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(paths)}
    engine = subprocess.check_output(["git", "-C", str(ROOT / "RobustToolbox"), "rev-parse", "HEAD"], text=True).strip()
    head = subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", "HEAD"], text=True).strip()
    value = {"head": head, "engine": engine, "files": files}
    value["sha256"] = hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()
    return value


if __name__ == "__main__":
    print(json.dumps(capture(), indent=2))
