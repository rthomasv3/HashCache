# Hash Cache

A simple project experimenting with the idea of caching values in a file instead of in memory. Hosting a machine with a large amount of RAM can be expensive, but disk space - even with a good SSD - is cheap.

But can it be fast enough? Initial results are promising when just using a mid-tier consumer SSD.

| Method | Mean     | Error    | StdDev   |
|------- |---------:|---------:|---------:|
| Set    | 25.27 us | 3.569 us | 10.52 us |
| Get    | 27.88 us | 4.199 us | 12.32 us |

## Two Ideas

### 1. Sparse File
The first idea was to try out a [sparse file](https://en.wikipedia.org/wiki/Sparse_file). By simply taking the hash of a key as a long, you can write directly to that index in the file without it actually taking up that much space on the disk.

One benefit of this approach is the simplicity of the code. To find out where a value is in the file you just take the hash times the block size and skip the header. For values that are larger than one block, you store the next block at the end (like a linked list), or zeros if another block isn't needed.

One big downside is portability - even in a docker container, support for sparse files depends on the host operating system. Sparse files can also require extra steps when copying or backing them up.


### 2. Index and Data Files
The second idea was to create an index file to track key locations in a file. This results in more complicated code and more than one file, but performance is still good and it's portable.

The file format is pretty straight forward for now, and there are a lot of ways it can be improved (like saving block size in header instead of hardcoded).

#### Index File
The version would be used in something like a factory to get a parser that can read and write the file correctly. Free blocks are used for deletes so areas of the file can be reused.

##### Header
| Data               | Type      |
| ------------------ | --------- |
| Version            | ushort    |
| Free Block Count   | int       |
| Free Block Indices | long[]    |
| Entry Count        | int       |
| Entries			 | Entry[]   |

##### Index Entry
| Data               | Type      |
| ------------------ | --------- |
| Key Hash           | ulong     |
| Expiration		 | byte[19]  |
| Block Count        | int       |
| Data Indices       | long[]    |
| Checksum           | ushort    |

#### Data File
The size is stored to prevent data corruption. A fixed block size means padding with zeros, so we need to know the difference between padding and valid ending zeros.

##### Block
| Data               | Type             |
| ------------------ | ---------------- |
| Data Size          | ushort           |
| Data               | byte[block size] |
