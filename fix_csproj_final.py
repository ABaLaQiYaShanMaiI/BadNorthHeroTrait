import os

base = r"f:\BadNorthProgram\BadNorthHeroTraits"

projects = [
    ("BadNorthAxeThrower", "BadNorthAxeThrower"),
    ("BadNorthCheaperClass", "BadNorthCheaperClass"),
    ("BadNorthRegenerative", "BadNorthRegenerative"),
    ("BadNorthThorns", "BadNorthThorns"),
]

for folder, name in projects:
    csproj = os.path.join(base, folder, name, f"{name}.csproj")
    with open(csproj, "r", encoding="utf-8") as f:
        content = f.read()
    
    # Remove System.Core reference
    content = content.replace('    <Reference Include="System.Core" />\n', "")
    content = content.replace('    <Reference Include="System.Core" />\r\n', "")
    
    # Fix BNAPI path
    old_path = r"f:\BadNorthProgram\BadNorthHeroTraits\BNAPI\BNAPI.dll"
    new_path = r"f:\BadNorthProgram\BadNorthHeroTraits\BNAPI\BNAPI\bin\Debug\BNAPI.dll"
    content = content.replace(old_path, new_path)
    
    with open(csproj, "w", encoding="utf-8") as f:
        f.write(content)
    
    print(f"Fixed: {csproj}")

print("All done!")
