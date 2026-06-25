import os

target_path = os.path.abspath(os.path.join(os.getcwd(), "..", "Activity Diagrams", "AD3_BossAI.puml"))
with open(target_path, "r", encoding="utf-8") as f:
    print(f.read())
