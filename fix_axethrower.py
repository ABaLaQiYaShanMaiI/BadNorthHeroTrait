import os

# Read the correct csproj from CheaperClass
with open(r'f:\BadNorthProgram\BadNorthHeroTraits\BadNorthCheaperClass\BadNorthCheaperClass\BadNorthCheaperClass.csproj', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace CheaperClass references with AxeThrower
content = content.replace('BadNorthCheaperClass', 'BadNorthAxeThrower')
content = content.replace('CheaperClass.cs', 'AxeThrower.cs')
content = content.replace('trait_cheaperclass.png', 'trait_axe.png')
content = content.replace('{C3D4E5F6-A7B8-9012-CDEF-123456789012}', '{B2C3D4E5-F6A7-8901-BCDE-F12345678901}')

# Add extra references for AxeThrower
ref_end = content.rfind('</Reference>')
insert_pos = content.find('</ItemGroup>', ref_end)
extra_refs = '''    <Reference Include="UnityEngine.AnimationModule">
      <HintPath>..\\..\\..\\BadNorth\\BadNorth_Data\\Managed\\UnityEngine.AnimationModule.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine.AudioModule">
      <HintPath>..\\..\\..\\BadNorth\\BadNorth_Data\\Managed\\UnityEngine.AudioModule.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine.PhysicsModule">
      <HintPath>..\\..\\..\\BadNorth\\BadNorth_Data\\Managed\\UnityEngine.PhysicsModule.dll</HintPath>
      <Private>False</Private>
    </Reference>
'''
content = content[:insert_pos] + extra_refs + content[insert_pos:]

with open(r'f:\BadNorthProgram\BadNorthHeroTraits\BadNorthAxeThrower\BadNorthAxeThrower\BadNorthAxeThrower.csproj', 'w', encoding='utf-8') as f:
    f.write(content)

print('Done')
