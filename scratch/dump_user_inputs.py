import json
import os

log_path = r"C:\Users\tanhv\.gemini\antigravity\brain\431b0b6a-9c2c-4fb0-9504-91fb4ddc814f\.system_generated\logs\transcript.jsonl"

user_inputs = []
with open(log_path, "r", encoding="utf-8") as f:
    for line in f:
        try:
            step = json.loads(line)
            if step.get("type") == "USER_INPUT":
                user_inputs.append(step)
        except Exception as e:
            pass

print(f"Total USER_INPUT steps: {len(user_inputs)}")
if user_inputs:
    latest = user_inputs[-1]
    content = latest.get("content", "")
    print("Latest USER_INPUT content length:", len(content))
    with open("scratch/latest_user_request.txt", "w", encoding="utf-8") as out:
        out.write(content)
    print("Wrote latest user input to scratch/latest_user_request.txt")
else:
    print("No USER_INPUT found.")
