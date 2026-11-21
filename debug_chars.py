import pathlib
p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Find the exact pattern
idx = text.find('else\n            {')
chunk = text[idx:idx+500]
print('EXACT CONTENT:')
print(repr(chunk[:400]))
print('---')
# Show the exact bytes
for i, ch in enumerate(chunk[:300]):
    if ch == '\n':
        print(f'{i}: \\n')
    elif ch == '\t':
        print(f'{i}: \\t')
    elif ch == '"':
        print(f'{i}: \\"')
    else:
        print(f'{i}: {ch}')