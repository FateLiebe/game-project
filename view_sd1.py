import os
import sys

sd_dir = os.path.abspath(os.path.join(os.getcwd(), "..", "Sequence Diagrams"))
file_path = os.path.join(sd_dir, "SD1_PlayerAttack_Detailed.puml")

if os.path.exists(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    sys.stdout.reconfigure(encoding='utf-8')
    print(content)
