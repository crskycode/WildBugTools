# WildBug Tools

This toolkit is designed for modifying games developed with the WildBug engine.

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
