# Simple Mafia CAR INDEX.DEF Editor 

### Purpose of the Program

This program reads and edits a `FileStructure` data structure containing car records (`CarRecord`), and then allows you to save the modified data back to a binary file. The program displays the structure’s fields and enables value changes, including selecting variants for enum fields from predefined lists.

---

### Environmental Requirements

1. **`carindex.def` file**  
   Must be located in the same folder as the executable (EXE).  
   The size and data format must match the structures described in the code.

2. **`headers.txt` file**  
   Must be located in the same folder as the EXE.  
   File format: each line is a `key=value` pair, where:
   - The `key` corresponds to the name of a structure field (e.g., `carNameId`, `enumA`, `zeroOrFour`, etc.)
   - The `value` is a human-readable name or description of that field.  
   
   These headers will be displayed on the form for the user’s convenience.

3. **`enums.txt` file**  
   Must be located in the same folder as the EXE.  
   File format:
   - Enum sections are marked as `[enum_name]`
   - Each section contains lines in the format `ItemName=Value`
   - Values can be in hexadecimal `0x..` or decimal format.

---

### Brief Description of the Workflow

- Upon startup, the application reads the structure from `carindex.def`.
- It then loads field headers from `headers.txt` and enum variants from `enums.txt`.
- The data of the first car (by default `index = 0`) is displayed on the form.
- The user can switch between cars (81 records total) using the `Next Car` and `Previous Car` buttons.
- The user can modify string fields, integer fields, and select from dropdown lists for enum fields.
- By clicking `Save`, the changes are written back into the structure and saved to the original binary file `carindex.def`.

---

### Licensing Notes

This code was generated by fucking GPT and then minimally modified by a human. The author makes no claims to any copyrights or ownership and does not particularly wish to be credited by name tbh. The code can be freely used, modified, copied, and distributed without any restrictions. 

You may consider this code to be distributed under the most open license possible, similar to public domain or an MIT/BSD-like license, with no significant limitations.

In other words: **no claims of authorship or rights are made**, and you are free to use this code for any purpose.


---

### Car Index 1.0 structure
```c

struct HeadRecord {
    u8 data[164];
};

struct EndRecord {
    u8 data[164];
};


struct CarRecord {
    char     carNameId[0x20];        // Car ID-name for scripts
    char     dsCrashModel[0x20]; // Car name after Explode
    char     dsShadowModel[0x20];// Car Shadow name
    char     FullCarName[0x20];  // Car name for ingame UI 

 // Additinal data
    u32  enumA1,enumA2;     
    u16 enumB; //Unk ENUM
    u8 zeroOrFour; // 00 or 04
    u8 zeroTWO; //always 02
    
    u32 enumC;
    u32 stealTimeMs; // Value between 10 до 7500
    u32 zero; // Always ZERO
    u32 unkB; // enumD
    u32 seatCount;
    u32 enumD; //emumE
};


struct FileStructure {
    HeadRecord start;
    CarRecord cars[10];
//    EndRecord end;
};



FileStructure carIndexV1_0 @ 0x00;

```

### 1.3 Structure
```c
struct HeadRecord {
    u8 data[200];
};

struct EndRecord {
    u8 data[200];
};


struct CarRecord {
    char     carNameId[0x20];        // Car ID-name for scripts
    char     dsCrashModel[0x20]; // Car name after Explode
    char     dsShadowModel[0x20];// Car Shadow name
    char     FullCarName[64];  // Car name for ingame UI 

 // Additinal data
    u32  enumA1,enumA2;     
    u16 enumB; //Unk ENUM
    u8 zeroOrFour; // 00 or 04
    u8 zeroTWO; //always 02
    
    u32 enumC;
    u32 stealTimeMs; // Value between 10 до 7500
    u32 zero; // Always ZERO
    u32 unkB; // enumD
    u32 seatCount;
    u32 enumD; //emumE
    u32 unkZ;
};


struct FileStructure {
    HeadRecord start;
    CarRecord cars[81];
    EndRecord end;
};



FileStructure carIndex @ 0x00;
```
