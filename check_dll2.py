import struct

def find_all(data, pattern):
    idx = 0
    results = []
    while True:
        idx = data.find(pattern, idx)
        if idx < 0:
            break
        results.append(idx)
        idx += 1
    return results

def analyze_dll(filepath):
    with open(filepath, 'rb') as f:
        data = f.read()
    
    print(f"=== {filepath} ===")
    print(f"Size: {len(data)} bytes")
    
    # Search for all type references in the metadata
    # In .NET PE files, type names are stored in the #Strings heap
    # Assembly references are stored in the AssemblyRef table
    
    # Look for the metadata root
    # The CLR header is at the data directory entry 14 (0xE8 from PE optional header)
    
    # First, find the PE signature
    pe_offset = struct.unpack('<I', data[0x3C:0x40])[0]
    
    # Get the number of data directories
    # PE32+ has optional header magic 0x20B, PE32 has 0x10B
    magic = struct.unpack('<H', data[pe_offset + 0x18:pe_offset + 0x1A])[0]
    
    if magic == 0x10B:  # PE32
        clr_header_offset = pe_offset + 0x18 + 0x60 + 14 * 8
    else:  # PE32+
        clr_header_offset = pe_offset + 0x18 + 0x70 + 14 * 8
    
    clr_rva = struct.unpack('<I', data[clr_header_offset:clr_header_offset + 4])[0]
    clr_size = struct.unpack('<I', data[clr_header_offset + 4:clr_header_offset + 8])[0]
    
    if clr_rva == 0:
        print("No CLR header found")
        return
    
    # Convert RVA to file offset
    # Parse section table
    section_offset = pe_offset + 0x18 + (0x60 if magic == 0x10B else 0x70) + 16 * 16
    num_sections = struct.unpack('<H', data[pe_offset + 0x06:pe_offset + 0x08])[0]
    
    sections = []
    for i in range(num_sections):
        sec_start = section_offset + i * 40
        name = data[sec_start:sec_start + 8].rstrip(b'\x00').decode('ascii', errors='replace')
        virtual_size = struct.unpack('<I', data[sec_start + 0x08:sec_start + 0x0C])[0]
        virtual_address = struct.unpack('<I', data[sec_start + 0x0C:sec_start + 0x10])[0]
        raw_size = struct.unpack('<I', data[sec_start + 0x10:sec_start + 0x14])[0]
        raw_offset = struct.unpack('<I', data[sec_start + 0x14:sec_start + 0x18])[0]
        sections.append((name, virtual_address, virtual_size, raw_offset, raw_size))
    
    def rva_to_offset(rva):
        for name, va, vs, ro, rs in sections:
            if va <= rva < va + vs:
                return ro + (rva - va)
        return None
    
    clr_offset = rva_to_offset(clr_rva)
    if clr_offset is None:
        print("Cannot find CLR header in file")
        return
    
    # Get metadata from CLR header
    meta_rva = struct.unpack('<I', data[clr_offset + 0x08:clr_offset + 0x0C])[0]
    meta_size = struct.unpack('<I', data[clr_offset + 0x0C:clr_offset + 0x10])[0]
    
    meta_offset = rva_to_offset(meta_rva)
    if meta_offset is None:
        print("Cannot find metadata in file")
        return
    
    # Parse metadata header
    # STORAGESIGNATURE
    sig = data[meta_offset:meta_offset + 4]
    if sig != b'BSJB':
        print(f"Invalid metadata signature: {sig}")
        return
    
    # Skip to stream headers
    # STORAGEHEADER
    major = data[meta_offset + 4]
    minor = data[meta_offset + 5]
    reserved = struct.unpack('<I', data[meta_offset + 0x06:meta_offset + 0x0A])[0]
    streams = struct.unpack('<I', data[meta_offset + 0x0A:meta_offset + 0x0E])[0]
    
    # Find #Strings and #~ streams
    pos = meta_offset + 0x0E
    streams_info = {}
    for i in range(streams):
        offset = struct.unpack('<I', data[pos:pos + 4])[0]
        size = struct.unpack('<I', data[pos + 4:pos + 8])[0]
        # Read stream name (null-terminated, padded to 4 bytes)
        name_start = pos + 8
        name_end = data.find(b'\x00', name_start)
        name = data[name_start:name_end].decode('ascii', errors='replace')
        streams_info[name] = (meta_offset + offset, size)
        # Move to next stream header (padded)
        pos = name_end + 1
        pos = (pos + 3) & ~3
    
    # Read #Strings heap
    if '#Strings' in streams_info:
        str_offset, str_size = streams_info['#Strings']
        strings_heap = data[str_offset:str_offset + str_size]
    else:
        print("No #Strings stream found")
        return
    
    # Read #~ (metadata tables) stream
    if '#~' in streams_info:
        tilde_offset, tilde_size = streams_info['#~']
        tilde_data = data[tilde_offset:tilde_offset + tilde_size]
    else:
        print("No #~ stream found")
        return
    
    # Parse #~ header
    # 0x00: reserved (1 byte)
    # 0x01: MajorVersion (1 byte)
    # 0x02: MinorVersion (1 byte)
    # 0x03: HeapSizes (1 byte)
    # 0x04: reserved (1 byte)
    # 0x05: Valid (8 bytes) - bitmask of present tables
    # 0x0D: Sorted (8 bytes) - bitmask of sorted tables
    
    heap_sizes = tilde_data[3]
    valid = struct.unpack('<Q', tilde_data[5:13])[0]
    
    # Table names
    table_names = [
        'Module', 'TypeRef', 'TypeDef', 'FieldPtr', 'Field', 'MethodPtr', 'Method',
        'ParamPtr', 'Param', 'InterfaceImpl', 'MemberRef', 'Constant', 'CustomAttribute',
        'FieldMarshal', 'DeclSecurity', 'ClassLayout', 'FieldLayout', 'StandAloneSig',
        'EventMap', 'EventPtr', 'Event', 'PropertyMap', 'PropertyPtr', 'Property',
        'MethodSemantics', 'MethodImpl', 'ModuleRef', 'TypeSpec', 'ImplMap',
        'FieldRVA', 'ENCLog', 'ENCMap', 'Assembly', 'AssemblyProcessor', 'AssemblyOS',
        'AssemblyRef', 'AssemblyRefProcessor', 'AssemblyRefOS', 'File', 'ExportedType',
        'ManifestResource', 'NestedClass', 'GenericParam', 'MethodSpec', 'GenericParamConstraint'
    ]
    
    # Parse table row counts
    table_pos = 13 + 8  # skip Valid and Sorted
    table_row_counts = {}
    for i in range(64):
        if valid & (1 << i):
            count = struct.unpack('<I', tilde_data[table_pos:table_pos + 4])[0]
            table_pos += 4
            table_row_counts[i] = count
    
    # Calculate string index size
    str_idx_size = 4 if (heap_sizes & 1) else 2
    
    # Read AssemblyRef table (table index 0x23 = 35)
    if 35 in table_row_counts:
        count = table_row_counts[35]
        print(f"\nAssemblyRef table: {count} entries")
        
        # AssemblyRef row layout:
        # MajorVersion (2), MinorVersion (2), BuildNumber (2), RevisionNumber (2)
        # Flags (4), PublicKeyOrToken (blob index), Name (string index), 
        # Culture (string index), HashValue (blob index)
        
        ref_pos = table_pos
        for j in range(count):
            # Skip version and flags
            ref_pos += 8 + 4
            # PublicKeyOrToken (blob index)
            ref_pos += 4 if (heap_sizes & 2) else 2
            # Name (string index)
            name_idx = struct.unpack('<I' if str_idx_size == 4 else '<H', 
                                      data[ref_pos:ref_pos + str_idx_size])[0]
            ref_pos += str_idx_size
            # Culture (string index)
            ref_pos += str_idx_size
            # HashValue (blob index)
            ref_pos += 4 if (heap_sizes & 2) else 2
            
            # Read the assembly name from strings heap
            name_end = strings_heap.find(b'\x00', name_idx)
            name = strings_heap[name_idx:name_end].decode('ascii', errors='replace')
            print(f"  [{j}] {name}")
    
    # Read TypeRef table (table index 1)
    if 1 in table_row_counts:
        count = table_row_counts[1]
        print(f"\nTypeRef table: {count} entries")
        
        # TypeRef row layout:
        # ResolutionScope (coded index), TypeName (string index), TypeNamespace (string index)
        
        # Calculate coded index size for ResolutionScope
        # ResolutionScope uses 2 bits to encode table: Module(0), ModuleRef(1), AssemblyRef(2), TypeRef(3)
        max_rows = max(table_row_counts.get(0, 0), table_row_counts.get(26, 0), 
                       table_row_counts.get(35, 0), table_row_counts.get(1, 0))
        coded_size = 4 if max_rows >= (1 << 14) else 2
        
        ref_pos = table_pos
        for j in range(count):
            # ResolutionScope
            ref_pos += coded_size
            # TypeName
            name_idx = struct.unpack('<I' if str_idx_size == 4 else '<H', 
                                      data[ref_pos:ref_pos + str_idx_size])[0]
            ref_pos += str_idx_size
            # TypeNamespace
            ns_idx = struct.unpack('<I' if str_idx_size == 4 else '<H', 
                                    data[ref_pos:ref_pos + str_idx_size])[0]
            ref_pos += str_idx_size
            
            name = strings_heap[name_idx:strings_heap.find(b'\x00', name_idx)].decode('ascii', errors='replace')
            ns = strings_heap[ns_idx:strings_heap.find(b'\x00', ns_idx)].decode('ascii', errors='replace')
            
            if 'Action' in name or 'Action' in ns:
                print(f"  [{j}] {ns}.{name}")
    
    # Read MemberRef table (table 10) - this is where method calls are referenced
    if 10 in table_row_counts:
        count = table_row_counts[10]
        print(f"\nMemberRef table: {count} entries")
        
        # MemberRef row layout:
        # Class (coded index), Name (string index), Signature (blob index)
        
        # Class coded index uses 3 bits: TypeDef(0), TypeRef(1), ModuleRef(2), MethodDef(3), TypeSpec(4)
        max_rows = max(table_row_counts.get(2, 0), table_row_counts.get(1, 0),
                       table_row_counts.get(26, 0), table_row_counts.get(6, 0),
                       table_row_counts.get(27, 0))
        coded_size = 4 if max_rows >= (1 << 13) else 2
        
        ref_pos = table_pos
        for j in range(count):
            # Class
            ref_pos += coded_size
            # Name
            name_idx = struct.unpack('<I' if str_idx_size == 4 else '<H', 
                                      data[ref_pos:ref_pos + str_idx_size])[0]
            ref_pos += str_idx_size
            # Signature
            ref_pos += 4 if (heap_sizes & 2) else 2
            
            name = strings_heap[name_idx:strings_heap.find(b'\x00', name_idx)].decode('ascii', errors='replace')
            
            if 'Action' in name or 'Invoke' in name or 'BeginInvoke' in name or 'EndInvoke' in name:
                print(f"  [{j}] {name}")

import sys
for dll in sys.argv[1:]:
    analyze_dll(dll)
    print()
