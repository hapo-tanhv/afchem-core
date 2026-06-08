import json
import os

log_path = r"C:\Users\tanhv\.gemini\antigravity\brain\431b0b6a-9c2c-4fb0-9504-91fb4ddc814f\.system_generated\logs\transcript.jsonl"

with open(log_path, "r", encoding="utf-8") as f:
    for i, line in enumerate(f):
        try:
            step = json.loads(line)
            content = step.get("content", "")
            if not content and "tool_calls" in step:
                # check tool call arguments
                content = str(step["tool_calls"])
            if "custom_thong_tin_bom_san_xuat_a" in content:
                print(f"Step {i}: type={step.get('type')} source={step.get('source')}")
                idx = content.find("custom_thong_tin_bom_san_xuat_a")
                print("Context:", repr(content[idx:idx+150]))
        except Exception as e:
            print(f"Error at step {i}: {e}")
