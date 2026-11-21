import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# The exact old pattern (from debug output)
old = '''else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\\"project_type:{type}\\"],[\\"versions:{gameVersion}\\"\\]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }'''

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

if old in text:
    text = text.replace(old, new, 1)
    print("✓ SearchAsync patched!")
else:
    print("NOT FOUND")

pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text, encoding='utf-8')
print("Done")