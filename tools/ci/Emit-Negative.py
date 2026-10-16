"""Manual CI qualification only: deliberately exits 19. Never used as a green gate."""
import subprocess
import sys

sys.exit(subprocess.call([*([] if sys.platform == "win32" else ["mono"]), sys.argv[1], "primary-cleanup"]))
