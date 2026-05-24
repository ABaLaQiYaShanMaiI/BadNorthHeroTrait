import struct

def read_assembly_refs(filepath):
    with open(filepath, 'rb') as f:
        data = f.read()
    
    # PE header offset
    pe_offset = struct.unpack('<I', data[0x3C:0x40])[0]
    
    # CLR Runtime Header (IMAGE_COR20_HEADER)
    clr_offset = struct.unpack('<I', data[pe_offset+0xE8:pe_offset+0xEC])[0]
    
    # Metadata
    meta_offset = struct.unpack('<I', data[clr_offset+8:clr_offset+12])[0]
    
    # Find all assembly references by searching for known assembly names
    assemblies = []
    for name in [b'mscorlib', b'System', b'System.Core', b'BNAPI', b'Assembly-CSharp', b'BepInEx', b'MMHOOK', b'UnityEngine']:
        count = data.count(name)
        if count > 0:
            assemblies.append((name.decode(), count))
    
    print(f"File: {filepath}")
    print(f"Size: {len(data)} bytes")
    print("Assembly references found:")
    for name, count in sorted(assemblies):
        print(f"  {name}: {count} occurrences")
    
    # Search for Action type reference
    # In .NET metadata, type names are stored as null-terminated UTF-8
    idx = 0
    action_refs = []
    while True:
        idx = data.find(b'System.Action', idx)
        if idx < 0:
            break
        # Check context
        ctx = data[max(0,idx-5):idx+30]
        action_refs.append(ctx)
        idx += 1
    
    if action_refs:
        print("\nSystem.Action references:")
        for r in action_refs:
            print(f"  {r}")
    else:
        print("\nNo System.Action references found")
    
    # Also check for Action without System. prefix
    idx = 0
    while True:
        idx = data.find(b'\x00Action\x00', idx)
        if idx < 0:
            break
        ctx = data[max(0,idx-10):idx+20]
        print(f"\nFound Action type at {idx}: {ctx}")
        idx += 1

import sys
for dll in sys.argv[1:]:
    read_assembly_refs(dll)
    print()
