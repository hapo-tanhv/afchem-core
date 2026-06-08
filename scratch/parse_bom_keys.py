import urllib.parse

with open("scratch/latest_user_request.txt", "r", encoding="utf-8") as f:
    text = f.read()

# Since the content in latest_user_request.txt might be urlencoded or direct query parameters, let's look for both:
# The user request is a raw query string or contains it. Let's find custom_thong_tin_bom_san_xuat_a=
def inspect_key(key):
    idx = text.find(key + "=")
    if idx == -1:
        print(f"Key {key} not found")
        return None
    start = idx + len(key) + 1
    # Find next & or the end of the text
    end = text.find("&", start)
    val = text[start:end] if end != -1 else text[start:]
    # If the user request is wrapped in some XML/HTML or tag, it might end with tag. Let's strip whitespace or tag endings if any.
    if val.endswith("</USER_REQUEST>"):
        val = val[:-len("</USER_REQUEST>")].strip()
    
    print(f"--- Key: {key} ---")
    print("Length of value:", len(val))
    print("First 100 chars:", val[:100])
    print("Last 100 chars:", val[-100:])
    
    # URL decode
    decoded = urllib.parse.unquote(val)
    print("Decoded length:", len(decoded))
    print("Decoded first 100 chars:", decoded[:100])
    print("Decoded last 100 chars:", decoded[-100:])
    return decoded

decoded_a = inspect_key("custom_thong_tin_bom_san_xuat_a")
decoded_b = inspect_key("custom_thong_tin_bom_san_xuat_b")
