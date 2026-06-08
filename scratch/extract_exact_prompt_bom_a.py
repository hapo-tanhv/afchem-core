with open("scratch/latest_user_request.txt", "r", encoding="utf-8") as f:
    text = f.read()

key = "custom_thong_tin_bom_san_xuat_a"
idx = text.find(key + "=")
if idx != -1:
    start = idx + len(key) + 1
    end = text.find("&", start)
    val = text[start:end] if end != -1 else text[start:]
    print("Exact length of custom_thong_tin_bom_san_xuat_a in file:", len(val))
    print("Last 150 chars in file:", repr(val[-150:]))
else:
    # If not found directly, search case insensitively
    print("Searching case-insensitively...")
    import re
    match = re.search(r"custom_thong_tin_bom_san_xuat_a\s*=\s*([^\s&]+)", text, re.IGNORECASE)
    if match:
        val = match.group(1)
        print("Found case-insensitively! Length:", len(val))
        print("Last 150 chars:", repr(val[-150:]))
    else:
        print("Not found anywhere in text.")
