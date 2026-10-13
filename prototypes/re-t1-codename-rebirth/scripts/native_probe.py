"""Read-only, bounded ARM64 native-loader disassembly and string xrefs."""
import argparse, json, struct, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'local/tools/python'))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN
from elftools.elf.elffile import ELFFile

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--library', default='libil2cpp.so')
    ap.add_argument('--string')
    ap.add_argument('--address', type=lambda x: int(x, 0))
    ap.add_argument('--length', type=lambda x: int(x, 0), default=512)
    args = ap.parse_args()
    p = ROOT / 'local/apk/lib/arm64-v8a' / args.library
    raw = p.read_bytes()
    with p.open('rb') as f:
        elf = ELFFile(f)
        segments = [(s['p_offset'], s['p_vaddr'], s['p_filesz']) for s in elf.iter_segments() if s['p_type'] == 'PT_LOAD']
        t = elf.get_section_by_name('.text')
        data, va = t.data(), t['sh_addr']
    md = Cs(CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN)
    def show(addr, count):
        chunk = None
        for o,v,n in segments:
            if v <= addr < v+n:
                chunk = raw[o+addr-v:o+addr-v+count]; break
        if chunk is None: raise ValueError(f'Unmapped address {addr:x}')
        for ins in md.disasm(chunk, addr):
            print(f'{ins.address:08x}: {ins.mnemonic:8} {ins.op_str}')
    if args.address is not None:
        show(args.address, args.length)
    if args.string:
        needle = args.string.encode('utf8')
        offs = []; pos = 0
        while (pos := raw.find(needle, pos)) >= 0:
            offs.append(pos); pos += len(needle)
        addrs = [v + x - o for x in offs for o,v,n in segments if o <= x < o+n]
        print('string', args.string, 'file offsets', [hex(x) for x in offs], 'VA', [hex(x) for x in addrs])
        pages = {x & ~4095 for x in addrs}
        for i, (w,) in enumerate(struct.iter_unpack('<I', data[:len(data)//4*4])):
            if w & 0x9f000000 != 0x90000000: continue
            imm = ((w >> 5 & 0x7ffff) << 2) | (w >> 29 & 3)
            if imm & 0x100000: imm -= 0x200000
            target = ((va + 4*i) & ~4095) + (imm << 12)
            if target not in pages: continue
            rd = w & 31
            for j in range(1, 9):
                if 4*(i+j+1)>len(data): break
                nxt = struct.unpack_from('<I', data, 4*(i+j))[0]
                if nxt & 0x7f000000 == 0x11000000 and (nxt >> 5 & 31) == rd:
                    a = target + (nxt >> 10 & 4095) * (4096 if nxt & 0x400000 else 1)
                    if a in addrs:
                        print('XREF', hex(va+4*i), hex(a)); show(va+4*i-32, 160)
                        break

if __name__ == '__main__': main()
