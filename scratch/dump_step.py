import json
import os

log_path = r"C:\Users\tanhv\.gemini\antigravity\brain\431b0b6a-9c2c-4fb0-9504-91fb4ddc814f\.system_generated\logs\transcript.jsonl"

with open(log_path, "r", encoding="utf-8") as f:
    for i, line in enumerate(f):
        if i == 122:
            step = json.loads(line)
            content = step.get("content", "")
            with open("scratch/user_input_122.txt", "w", encoding="utf-8") as out:
                out.write(content)
            print("Successfully dumped step 122 content to scratch/user_input_122.txt")
            break
