import base64
import urllib.parse

# This is the string for BOM A directly from the prompt
b64_str_a = "W1siS0VPLTAxNCIsIkFGLTAxIiwiNTc0LjIxIiwiNTc0LjIxIiwia2ciLCIyMDI2MDMzMTExRiJdLFsiQUNJRC0wMjAiLCJBRi0zMCIsIjI0Ljc3IiwiMjQuNzciLCJrZyIsIjg4MjYwMDAwMTAiXSxbIkFDSUQtMDE3IiwiQUYtMTAiLCI3Ny43MiIsIjc3LjcyIiwia2ciLCIzMzI1MTIwODEiXSxbIkFDSUQt0DIzIiwiQUYtNDQiLCI4LjYiLCI4LjYiLCJrZyIsIlM1MTAwMjgiXSxbIi0iLCJQcmVtaXggUlMwMjUtMSIsIjY1LjMiLCI6NS4zIiwia2ciLCJtXHUwMGUzIGhcdTAwZjNhIl0sWyJOVkwtMDA2IiwiVFx1MDBmYWkgbmlsb24gNTV4MTAwICggdFx1MDBmYWkgbFx1MWVkM25nIHRyb25nKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwiMTgxMjIwMjUiXSxbIi0iLCJUZW0gQ2FycmFnZWVuYW4gQUYzMDAiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0wNDYiLCJCYW8gVHJcdTFlYWZuZyBPREsiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0xNzgiLCJEXHUwMGUyeSB0aFx1MDBlZHQgNCoyMDAgbW0gKDUwMCBjXHUwMGUxaVwvdFx1MDBmYWkpIiwiMzAiLCIzMCIsImNoaVx1MWViZmMiLCItIl0sWyJOVkwtMDA3IiwiVFx1MDBmYWkgbmlsb24gN00eMTIwICggdFx1MDBmYWkgbFx1MWVkM25nIG5nb1x1MDBlMGkgKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwi260528Il1d"

print("Original length:", len(b64_str_a))

# Let's check characters that are not in the base64 alphabet [A-Za-z0-9+/=]
valid_alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="
invalid_chars = []
for i, c in enumerate(b64_str_a):
    if c not in valid_alphabet:
        invalid_chars.append((i, c))

print("Invalid Base64 characters found:", invalid_chars)

# Let's try to decode prefixes to see where it breaks down
for length in range(4, len(b64_str_a) + 1, 4):
    try:
        decoded = base64.b64decode(b64_str_a[:length].encode('ascii'))
        # Try to print as string, handle errors
        try:
            str_val = decoded.decode('utf-8')
            # Success
        except UnicodeDecodeError:
            # We decoded raw bytes but they're not valid utf-8
            pass
    except Exception as e:
        print(f"Decoding failed at length {length}: {e}")
        break

# Let's inspect specific parts of the string
# "QUNJRC00DIz" -> Wait, 0DIz contains '0' which is in base64 alphabet, but does it decode correctly?
# Wait! Let's decode block by block and print.
try:
    # Let's replace non-alphabetic chars or try to decode it with errors='replace' or similar
    # Actually, let's print the binary decode of the whole string, replacing bad bytes
    decoded_bytes = base64.b64decode(b64_str_a, validate=False)
    print("Decoded raw bytes (ignoring validation):", decoded_bytes[:200])
    print("Decoded utf-8 (with errors='replace'):", decoded_bytes.decode('utf-8', errors='replace'))
except Exception as e:
    print("Decoded validate=False failed:", e)
