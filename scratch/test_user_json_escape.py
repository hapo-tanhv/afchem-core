import base64

expected_json_escaped = '[["KEO-014","AF-01","574.21","574.21","kg","2026033111F"],["ACID-020","AF-30","24.77","24.77","kg","8826000010"],["ACID-017","AF-10","77.72","77.72","kg","332512081"],["ACID-023","AF-44","8.6","8.6","kg","S510028"],["-","Premix RS025-1","65.3","65.3","kg","m\\u00e3 h\\u00f3a"],["NVL-006","T\\u00fai nilon 55x100 ( t\\u00fai l\\u1ed3ng trong)","30","30","chi\\u1ebfc","18122025"],["-","Tem Carrageenan AF300","30","30","chi\\u1ebfc","-"],["NVL-046","Bao Tr\\u1eafng ODK","30","30","chi\\u1ebfc","-"],["NVL-178","D\\u00e2y th\\u00edt 4*200 mm (500 c\\u00e1i\\/t\\u00fai)","30","30","chi\\u1ebfc","-"],["NVL-007","T\\u00fai nilon 70x120 ( t\\u00fai l\\u1ed3ng ngo\\u00e0i )","30","30","chi\\u1ebfc","260528"]]'

correct_b64 = base64.b64encode(expected_json_escaped.encode('utf-8')).decode('utf-8')
print("Correct (escaped) Base64 length:", len(correct_b64))

user_b64 = "W1siS0VPLTAxNCIsIkFGLTAxIiwiNTc0LjIxIiwiNTc0LjIxIiwia2ciLCIyMDI2MDMzMTExRiJdLFsiQUNJRC0wMjAiLCJBRi0zMCIsIjI0Ljc3IiwiMjQuNzciLCJrZyIsIjg4MjYwMDAwMTAiXSxbIkFDSUQtMDE3IiwiQUYtMTAiLCI3Ny43MiIsIjc3LjcyIiwia2ciLCIzMzI1MTIwODEiXSxbIkFDSUQt0DIzIiwiQUYtNDQiLCI4LjYiLCI4LjYiLCJrZyIsIlM1MTAwMjgiXSxbIi0iLCJQcmVtaXggUlMwMjUtMSIsIjY1LjMiLCI6NS4zIiwia2ciLCJtXHUwMGUzIGhcdTAwZjNhIl0sWyJOVkwtMDA2IiwiVFx1MDBmYWkgbmlsb24gNTV4MTAwICggdFx1MDBmYWkgbFx1MWVkM25nIHRyb25nKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwiMTgxMjIwMjUiXSxbIi0iLCJUZW0gQ2FycmFnZWVuYW4gQUYzMDAiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0wNDYiLCJCYW8gVHJcdTFlYWZuZyBPREsiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0xNzgiLCJEXHUwMGUyeSB0aFx1MDBlZHQgNCoyMDAgbW0gKDUwMCBjXHUwMGUxaVwvdFx1MDBmYWkpIiwiMzAiLCIzMCIsImNoaVx1MWViZmMiLCItIl0sWyJOVkwtMDA3IiwiVFx1MDBmYWkgbmlsb24gN00eMTIwICggdFx1MDBmYWkgbFx1MWVkM25nIG5nb1x1MDBlMGkgKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwi260528Il1d"
if len(user_b64) % 4 != 0:
    user_b64 += "=" * (4 - len(user_b64) % 4)

print("User Base64 length:", len(user_b64))

# Find differences character-by-character
min_len = min(len(correct_b64), len(user_b64))
diffs = []
for i in range(min_len):
    if correct_b64[i] != user_b64[i]:
        diffs.append((i, correct_b64[i], user_b64[i]))

print(f"Number of differences: {len(diffs)}")
for idx, c_char, u_char in diffs:
    print(f"Diff at index {idx}: Expected '{c_char}', got '{u_char}'")
    context_start = max(0, idx - 10)
    context_end = min(min_len, idx + 10)
    print(f"  Expected: {correct_b64[context_start:idx]}[{c_char}]{correct_b64[idx+1:context_end]}")
    print(f"  User:     {user_b64[context_start:idx]}[{u_char}]{user_b64[idx+1:context_end]}")
