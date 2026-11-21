import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Find the exact patterns
idx = text.find('else\n            {')
print(f"Found else at {idx}")
if idx >= 0:
    context = text[idx:idx+300]
    print("Context:")
    print(repr(context[:200]))

# Let's do a more targeted fix
text2 = text.replace(
    '''else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\"project_type:{type}\"],[\"versions:{gameVersion}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }''',
    '''else if (_category == ModrinthCategory.Modpacks)
            {
                hits = await Modrinth.SearchModpacksAsync(query, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\"project_type:{type}\"],[\"versions:{gameVersion}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }''')

# Check if replacement happened
if text2 != text:
    print("SearchAsync replaced!")
else:
    print("NOT REPLACED - exact match failed")
    # Let's find the exact text
    idx = text.find('else\n            {')
    print(repr(text[idx:idx+400]))

pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text2, encoding='utf-8')
print("Done")