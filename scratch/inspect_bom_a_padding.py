import base64

b64_str_a = "W1siS0VPLTAxNCIsIkFGLTAxIiwiNTc0LjIxIiwiNTc0LjIxIiwia2ciLCIyMDI2MDMzMTExRiJdLFsiQUNJRC0wMjAiLCJBRi0zMCIsIjI0Ljc3IiwiMjQuNzciLCJrZyIsIjg4MjYwMDAwMTAiXSxbIkFDSUQtMDE3IiwiQUYtMTAiLCI3Ny43MiIsIjc3LjcyIiwia2ciLCIzMzI1MTIwODEiXSxbIkFDSUQt0DIzIiwiQUYtNDQiLCI4LjYiLCI4LjYiLCJrZyIsIlM1MTAwMjgiXSxbIi0iLCJQcmVtaXggUlMwMjUtMSIsIjY1LjMiLCI6NS4zIiwia2ciLCJtXHUwMGUzIGhcdTAwZjNhIl0sWyJOVkwtMDA2IiwiVFx1MDBmYWkgbmlsb24gNTV4MTAwICggdFx1MDBmYWkgbFx1MWVkM25nIHRyb25nKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwiMTgxMjIwMjUiXSxbIi0iLCJUZW0gQ2FycmFnZWVuYW4gQUYzMDAiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0wNDYiLCJCYW8gVHJcdTFlYWZuZyBPREsiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0xNzgiLCJEXHUwMGUyeSB0aFx1MDBlZHQgNCoyMDAgbW0gKDUwMCBjXHUwMGUxaVwvdFx1MDBmYWkpIiwiMzAiLCIzMCIsImNoaVx1MWViZmMiLCItIl0sWyJOVkwtMDA3IiwiVFx1MDBmYWkgbmlsb24gN00eMTIwICggdFx1MDBmYWkgbFx1MWVkM25nIG5nb1x1MDBlMGkgKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwi260528Il1d"

rem = len(b64_str_a) % 4
if rem > 0:
    b64_str_a += "=" * (4 - rem)

try:
    decoded_bytes = base64.b64decode(b64_str_a)
    decoded_str = decoded_bytes.decode('utf-8', errors='replace')
    with open("scratch/decoded_bom_a.txt", "w", encoding="utf-8") as f:
        f.write(decoded_str)
    print("Successfully wrote decoded BOM A to scratch/decoded_bom_a.txt")
except Exception as e:
    print("Decoding/writing failed:", e)
