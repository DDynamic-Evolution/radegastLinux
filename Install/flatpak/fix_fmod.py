import struct
with open('/app/bin/libfmod.so', 'rb+') as f:
    d = f.read()
    ec = d[4]
    p_off, p_size, n_off = (32, 8, 56) if ec == 2 else (28, 4, 44)
    phoff = struct.unpack('<Q', d[p_off:p_off + p_size])[0] if ec == 2 else struct.unpack('<I', d[28:32])[0]
    e_phnum = struct.unpack('<H', d[n_off:n_off + 2])[0]
    for i in range(e_phnum):
        off = phoff + i * (56 if ec == 2 else 32)
        p_type = struct.unpack('<I', d[off:off + 4])[0]
        if p_type == 0x6474e551:
            flags = struct.unpack('<I', d[off + 4:off + 8])[0]
            new_flags = flags & ~1
            f.seek(off + 4)
            f.write(struct.pack('<I', new_flags))
            print(f'Cleared PF_X on PT_GNU_STACK: 0x{flags:x} -> 0x{new_flags:x}')
            break
