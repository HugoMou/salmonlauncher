import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Find the exact block starting from "if (file is null)"
idx = text.find('if (file is null)')
chunk = text[idx:idx+800]
print("FOUND AT:", idx)
print(repr(chunk[:800]))

# Find the end - look for the end of the class
end_idx = text.find('    }', idx)
print("First closing brace after:", end_idx)

# Let's find the exact end of the InstallAsync method
# Look for "    }" that closes the class
idx_class_end = text.rfind('    }\n}')
print("Class end at:", idx_class_end)