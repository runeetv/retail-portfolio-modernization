"""Check that every YAML file under the given folders parses.

Usage (from the repository root):
    python docs/tutorial/tools/validate_yaml.py analysis-output consolidation-planning

Requires: pip install pyyaml
Exit code 0 when all files parse, 1 otherwise.
"""
import glob
import os
import sys

import yaml


def main(folders):
    folders = folders or ["."]
    files = sorted(
        f
        for folder in folders
        for pattern in ("**/*.yaml", "**/*.yml")
        for f in glob.glob(os.path.join(folder, pattern), recursive=True)
    )
    failures = 0
    for path in files:
        try:
            with open(path, encoding="utf-8") as fh:
                yaml.safe_load(fh)
        except yaml.YAMLError as err:
            failures += 1
            print(f"FAIL  {path}\n      {str(err).splitlines()[0]}")
    print(f"Checked {len(files)} YAML file(s): {len(files) - failures} OK, {failures} failed.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
