# mftf
This tool searches for filenames and alternate data streams names by parsing the content of the MFT in a
live system or in a mounted (read-only included) logical drive.

The results are shown with the filetime information from the attributes $SI and $FN and the sizes.

You can also copy files or alternate data streams using the data or ADS references provided in the results.

The copy is made reading directly the clusters in use. You can copy protected system files or files in use.

The methods used are obtained from KERNEL32.DLL library: GetVolumeInformationByHandleW, ReadFile, CreateFile, SetFilePointerEx, GetFileInformationByHandle, DeviceIoControl.


Options:

    -d drive_letter............................Search/copy files from this logical unit.
    -h........................................This help.
    -f "string1|string2 with spaces|string3?...".....Find file/directory/ADS names with any of the strings.
    -f "d:\folder\string"                         .....The path will limit the results to the subfolders. 
                The match is always case insensitive.
                " as delimiters for the whole group of strings.
                | is the boundary between strings.
                ? al the end of the string specifies an exact coincidence.
    -ff file.txt....................The strings to search for are in file.txt.
                                    One string per line, no separator, use ? as needed.
    -fr string......................Find the string doing a raw search in the 1024 bytes of the MFT record.
                                    It will report coincidences in the unallocated space of the MFT record.
    -fads...........................Find all the ADSs in the logical unit.
    >Can be used with any of the previous find options:
        -fx..................................Save the results in a file in order to use the option -c.
        -ft..................................Show the results in timeline format.
    -i full_path_to_file/directory.......Show information about the path.
    -i record_number.....................Show information of the MFT record.
    -w record_number.....................Write on screen the 1024 bytes of the MFT record.
    -c "reference1|reference2..."......Copy the file/s referenced to this folder.
                                           | is the separator.
    -c list.txt..........................Copy all the files referenced in the file list.txt.
                                           Each line MUST start with: reference + [TAB].
    -cr record_number....................Copy the 1024 bytes of the MFT record to this folder.

Examples:

MFT-fileoper.exe -d e: -f "svchost|mvui.dll|string with spaces|exact match?"

MFT-fileoper.exe -d e -fx -f "c:\folder\temp.dll|snbclog.exe"

MFT-fileoper.exe -d e -c "33:128-1|5623:128-4"
