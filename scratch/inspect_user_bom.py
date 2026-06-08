import urllib.parse
import os

with open("scratch/user_input_122.txt", "r", encoding="utf-8") as f:
    text = f.read()

print("User Input total length:", len(text))

# Let's search for "custom_thong_tin_bom_san_xuat_a="
idx = text.find("custom_thong_tin_bom_san_xuat_a=")
if idx != -1:
    start = idx + len("custom_thong_tin_bom_san_xuat_a=")
    end = text.find("&", start)
    val_encoded = text[start:end] if end != -1 else text[start:]
    print("Encoded BOM A length:", len(val_encoded))
    print("Encoded BOM A start:", val_encoded[:100])
    
    val_decoded = urllib.parse.unquote(val_encoded)
    print("Decoded BOM A length:", len(val_decoded))
    print("Decoded BOM A start:", val_decoded[:100])
    print("Decoded BOM A end:", val_decoded[-100:])
    
    # Check if there are backslashes
    print("Backslash count in decoded:", val_decoded.count('\\'))
    
    # Print character codes around the backslashes
    for i, c in enumerate(val_decoded):
        if c == '\\':
            print(f"Backslash at index {i}, surrounding: {repr(val_decoded[max(0, i-10):min(len(val_decoded), i+15)])}")
else:
    print("Not found custom_thong_tin_bom_san_xuat_a=")
