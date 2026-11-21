import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Find the exact byte sequence
idx = text.find('else\n            {')
chunk = text[idx:idx+500]

# The exact old string as it appears in the file
old = text[idx:idx+400]
print("OLD LENGTH:", len(old))
print(repr(old))

# Now construct new with the exact same formatting
new = '''else if (_category == ModrinthCategory.Modpacks)
            {
                hits = await Modrinth.SearchModpacksAsync(query, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\\"project_type:{type}\\"],[\\"versions:{gameVersion}\\"\\]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }'''

text = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').read_text(encoding='utf-8')

# Use the exact slice
start = text.find('else\n            {')
if start >= 0:
    # Find the end of this block
    end = text.find('\n            }\n\n            Rows.Clear();', start)
    if end >= 0:
        old_exact = text[start:end+1]  # include the closing brace and newline
        print(f"Found block length: {len(old_exact)}")
        print(repr(old_exact[:200]))
        
        # Replace
        new_text = text[:start] + '''else if (_category == ModrinthCategory.Modpacks)
            {
                hits = await Modrinth.SearchModpacksAsync(query, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\\"project_type:{type}\\"],[\\"versions:{gameVersion}\\"\\]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }''' + text[end+1:]
        pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(new_text, encoding='utf-8')
        print("✓ Replaced!")
    else:
        print("Could not find end marker")
else:
    print("Could not find start")