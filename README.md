# WildBug Tools

This toolkit is designed for modifying games developed with the WildBug engine.

## Resource Packs

Resource pack files for this engine have the `.WBP` extension, and their file header bytes are `ARCFORM4`.

### Extracting Files from a Resource Pack

Run the following command:
```
ArcTool -e -in input.WBP -out Extract -cp shift_jis
```

Parameter Description:
- `-e`: Extract files from the resource pack.
- `-in`: Specify the resource pack filename.
- `-out`: Specify the directory where extracted items will be stored.
- `-cp`: Specify the encoding of filenames within the resource pack. This is usually `shift_jis`.

### Creating a Resource Pack

Run the following command:
```
ArcTool -c -in RootDirectory -out res.WBP -cp shift_jis
```

Parameter Description:
- `-c`: Create a resource pack.
- `-in`: Specify the folder containing files you wish to add to the resource pack.
- `-out`: Specify the resource pack filename.
- `-cp`: Specify the encoding of filenames within the resource pack. This is usually `shift_jis`.

## Scripts

Script files for this engine have the `.WBI` or `.SCN` extension and have an identifier `WPX.EX2` or `WBUG_SCN` in the file header.

### Disassembling Scripts

Run the following command:
```
ScriptTool -d -in input.SCN -icp shift_jis -out output.txt
```

Parameter Description:
- `-d`: Disassemble analysis.
- `-in`: Specify the script filename.
- `-icp`: Specify the encoding of text within the script file. This is usually `shift_jis`.
- `-out`: Specify the output filename.

### Extracting Text from Scripts

Run the following command:
```
ScriptTool -e -in input.SCN -icp shift_jis -out output.txt
```

Parameter Description:
- `-e`: Extract text.
- `-in`: Specify the script filename.
- `-icp`: Specify the encoding of text within the script file. This is usually `shift_jis`.
- `-out`: Specify the output filename.

### Importing Text into Scripts

Run the following command:
```
ScriptTool -i -in input.SCN -icp shift_jis -out output.SCN -ocp shift_jis -txt input.TXT -p
```

Parameter Description:
- `-i`: Import text.
- `-in`: Specify the script filename.
- `-icp`: Specify the encoding of text within the script file. This is usually `shift_jis`.
- `-out`: Specify the output filename.
- `-ocp`: Specify the encoding of text within the output script file. This is usually `shift_jis`.
- `-txt`: Specify the filename of the file containing text you wish to import.
- `-p`: Package the script in WPX format (optional).

---

**Note:** This toolkit has been tested on a limited number of games only.
