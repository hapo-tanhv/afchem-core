import base64

# Let's find what the correct base64 is for BOM A
# Here is the target BOM A data structure, which is similar to BOM B but with BOM A values:
# Looking at the user request:
# custom_bang_dinh_muc_nvl shows:
# Row 1: AF-01 (from BOM A: 574.21, from BOM B: 382.96, Total: 957.17)
# Row 2: AF-30 (from BOM A: 24.77, from BOM B: 16.52, Total: 41.29)
# Row 3: AF-10 (from BOM A: 77.72, from BOM B: 51.81, Total: 129.53)
# Row 4: AF-44 (from BOM A: 8.6, from BOM B: 5.76, Total: 14.36)
# Row 5: Premix RS025-1 (from BOM A: 65.3, from BOM B: 43.55, Total: 108.85)
# Row 7: Túi nilon 55x100 (from BOM A: 30, from BOM B: 20, Total: 50)
# Row 8: Dây thít 4*200 mm (from BOM A: 30, from BOM B: 20, Total: 50)
# Row 9: Tem Carrageenan AF300 (from BOM A: 30, from BOM B: 20, Total: 50)
# Row 10: Bao ổn định kem (from BOM A: 30, from BOM B: 20, Total: 50) -> wait, in BOM A this is Bao Trắng ODK? Or Bao ổn định kem?
# Let's look at BOM A names:
# ["KEO-014","AF-01","574.21","574.21","kg","2026033111F"]
# ["ACID-020","AF-30","24.77","24.77","kg","8826000010"]
# ["ACID-017","AF-10","77.72","77.72","kg","332512081"]
# ["ACID-023","AF-44","8.6","8.6","kg","S510028"]
# ["-","Premix RS025-1","65.3","65.3","kg","mã hóa"]
# ["NVL-006","Túi nilon 55x100 ( túi lồng trong)","30","30","chiếc","18122025"]
# ["-","Tem Carrageenan AF300","30","30","chiếc","-"]
# ["NVL-046","Bao Trắng ODK","30","30","chiếc","-"]
# ["NVL-178","Dây thít 4*200 mm (500 cái/túi)","30","30","chiếc","-"]
# ["NVL-007","Túi nilon 70x120 ( túi lồng ngoài )","30","30","chiếc","260528"]

correct_json = '[["KEO-014","AF-01","574.21","574.21","kg","2026033111F"],["ACID-020","AF-30","24.77","24.77","kg","8826000010"],["ACID-017","AF-10","77.72","77.72","kg","332512081"],["ACID-023","AF-44","8.6","8.6","kg","S510028"],["-","Premix RS025-1","65.3","65.3","kg","mã hóa"],["NVL-006","Túi nilon 55x100 ( túi lồng trong)","30","30","chiếc","18122025"],["-","Tem Carrageenan AF300","30","30","chiếc","-"],["NVL-046","Bao Trắng ODK","30","30","chiếc","-"],["NVL-178","Dây thít 4*200 mm (500 cái/túi)","30","30","chiếc","-"],["NVL-007","Túi nilon 70x120 ( túi lồng ngoài )","30","30","chiếc","260528"]]'

correct_b64 = base64.b64encode(correct_json.encode('utf-8')).decode('utf-8')
print("Correct Base64 length:", len(correct_b64))
print("Correct Base64 string:")
print(correct_b64)

# Let's check how the user's base64 matches or differs
user_b64 = "W1siS0VPLTAxNCIsIkFGLTAxIiwiNTc0LjIxIiwiNTc0LjIxIiwia2ciLCIyMDI2MDMzMTExRiJdLFsiQUNJRC0wMjAiLCJBRi0zMCIsIjI0Ljc3IiwiMjQuNzciLCJrZyIsIjg4MjYwMDAwMTAiXSxbIkFDSUQtMDE3IiwiQUYtMTAiLCI3Ny43MiIsIjc3LjcyIiwia2ciLCIzMzI1MTIwODEiXSxbIkFDSUQt0DIzIiwiQUYtNDQiLCI4LjYiLCI4LjYiLCJrZyIsIlM1MTAwMjgiXSxbIi0iLCJQcmVtaXggUlMwMjUtMSIsIjY1LjMiLCI6NS4zIiwia2ciLCJtXHUwMGUzIGhcdTAwZjNhIl0sWyJOVkwtMDA2IiwiVFx1MDBmYWkgbmlsb24gNTV4MTAwICggdFx1MDBmYWkgbFx1MWVkM25nIHRyb25nKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwiMTgxMjIwMjUiXSxbIi0iLCJUZW0gQ2FycmFnZWVuYW4gQUYzMDAiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0wNDYiLCJCYW8gVHJcdTFlYWZuZyBPREsiLCIzMCIsIjMwIiwiY2hpXHUxZWJmYyIsIi0iXSxbIk5WTC0xNzgiLCJEXHUwMGUyeSB0aFx1MDBlZHQgNCoyMDAgbW0gKDUwMCBjXHUwMGUxaVwvdFx1MDBmYWkpIiwiMzAiLCIzMCIsImNoaVx1MWViZmMiLCItIl0sWyJOVkwtMDA3IiwiVFx1MDBmYWkgbmlsb24gN00eMTIwICggdFx1MDBmYWkgbFx1MWVkM25nIG5nb1x1MDBlMGkgKSIsIjMwIiwiMzAiLCJjaGlcdTFlYmZjIiwi260528Il1d"
# Pad user_b64
if len(user_b64) % 4 != 0:
    user_b64 += "=" * (4 - len(user_b64) % 4)

print("Padded user base64 length:", len(user_b64))

# Find differences character-by-character
min_len = min(len(correct_b64), len(user_b64))
diffs = []
for i in range(min_len):
    if correct_b64[i] != user_b64[i]:
        diffs.append((i, correct_b64[i], user_b64[i]))

print(f"Number of differences up to min length: {len(diffs)}")
for idx, c_char, u_char in diffs[:10]:
    # Print context around diff
    context_start = max(0, idx - 10)
    context_end = min(min_len, idx + 10)
    print(f"Diff at index {idx}: Expected '{c_char}', got '{u_char}'")
    print(f"  Expected context: {correct_b64[context_start:idx]}[{c_char}]{correct_b64[idx+1:context_end]}")
    print(f"  User context:     {user_b64[context_start:idx]}[{u_char}]{user_b64[idx+1:context_end]}")
