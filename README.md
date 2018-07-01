Search and copy files and ADS,s parsing the $MFT and reading directly the data from the clusters.

Two timeline formats suported to make the timeline ($SI and $FN times) of a live or offline $MFT ( 30 seconds 900k records):

	Option -tl: Date\tTime\t[MACB]\tfilename\trecord\tsize
	Option -l2t (Plaso/log2timeline): date,time,timezone,MACB,source,sourcetype,type,user,host,short,desc,version,filename,inode,notes,format,extra

You can filter the output searching from and/or to a date.

The tool can parse the $MFT from a live system, from a mounted (read-only included) logical drive or from a copy of the $MFT.

Deleted files and folders have their path with the prefix "?".

It can copy files by filename or files and ADS,s using the references provided in the search results.

The copy is made by reading the data from the clusters so that you can copy protected system files or files in use.
(Imports from "kernel32.dll":	CloseHandle, CreateFile, ReadFile, SetFilePointerEx).

The initial delay is due to the creation of a dictionary with full file paths.

Examples:

Sparse chunks are not copied. This is very useful when copying the $Usnjrnl:$J. Here the real size of the $J was 19 Gb but 
only 470 Mb had content while the rest were sparse chunks. The final file was only 470 Mb in size:

>mftf -cp c:\$extend\$usnjrnl:$j -n d:\data\$j.dat

Volume serial number: 2314228289244169585
Sector size: 4096 bytes
Cluster size: 4096 bytes
Record size: 4096 bytes
Starting cluster of the MFT: 4 [Offset: 0x4000]
Records: 602,919
Writing run length: 0 [Real file size: 18,988,733,296 bytes].
Sparse chunk not saved: 0 bytes.
Writing run length: 0 [Real file size: 18,988,733,296 bytes].
Sparse chunk not saved: 0 bytes.
Writing run length: 69,926,912 [Real file size: 18,988,733,296 bytes].
Writing run length: 109,260,800 [Real file size: 18,988,733,296 bytes].
Writing run length: 33,017,856 [Real file size: 18,988,733,296 bytes].
File copied to: d:\data\$j.dat
Copy finished: d:\data\$j.dat
...........
Using the tl format and SHA1:

mftf -d c -f shadow1 -tl -sha1

	2017/05/05,18:56:50.2977651,SI[MACB],C:\Program Files\soda_shadow1.ffm,113504,588,C6646B28833C4FFB7BC4E10256C134D290C7D419
	2016/09/15,12:03:29.0766109,FN[M...],C:\Program Files\soda_shadow1.ffm,113504,588,C6646B28833C4FFB7BC4E10256C134D290C7D419
	2016/09/15,11:56:41.9917419,FN[.A.B],C:\Program Files\soda_shadow1.ffm,113504,588,C6646B28833C4FFB7BC4E10256C134D290C7D419
	2017/04/13,19:09:28.2249511,FN[..C.],C:\Program Files\soda_shadow1.ffm,113504,588,C6646B28833C4FFB7BC4E10256C134D290C7D419

In this example the file has 4 $FN attributes and two ADS and the Attribute List points to another record.

mftf -d c -i 623677

	Record: 623677 [Attribute List points to records numbers: 623745]
	[File]  \\_SMSVC~1.INI
	[File]  \\_SMSvcHostPerfCounters_D.ini
	[File]  \\_SMSvcHostPerfCounters_D.ini
	[File]  \\_SMSvcHostPerfCounters_D.ini
	SI[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/08/23 12:01:53.1659607   2014/02/18 07:51:58.3286194
	FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194
	FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194
	FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:52:00.2179972   2014/02/18 07:51:58.3286194
	FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/08/23 12:01:53.1503598   2014/02/18 07:51:58.3286194
	Reference: 623677:128-1 [Size: 41 bytes|| Size on disk: 0 bytes]
	[ADS] Name: hmx33t [Reference: 623677:128-2 || Size: 1069547520 bytes]
	[ADS] Name: Zone.Identifier [Reference: 623677:128-3 || Size: 23 bytes]

The same file in the timeline format with dates and times from all the $FN attributes.
The dates and times of the ADS are those of the $SI attribute.

mftf -d c -f "_SMSvcHostPerfCounters_D" -t

	Filetime,[MACB],filename,record,size
	2014/02/18 07:51:58.3286194,SI[MA.B],\\_SMSvcHostPerfCounters_D.ini,623677,41
	2014/08/23 12:01:53.1659607,SI[..C.],\\_SMSvcHostPerfCounters_D.ini,623677,41
	2014/02/18 07:51:58.3286194,FN[MACB],\\_SMSvcHostPerfCounters_D.ini,623677,41
	2014/02/18 07:51:58.3286194,FN[MA.B],\\_SMSvcHostPerfCounters_D.ini,623677,41
	2014/02/18 07:52:00.2179972,FN[..C.],\\_SMSvcHostPerfCounters_D.ini,623677,41
	2014/08/23 12:01:53.1503598,FN[..C.],\\_SMSvcHostPerfCounters_D.ini,623677,41
	2014/02/18 07:51:58.3286194,SI[MA.B],\\_SMSvcHostPerfCounters_D.ini:hmx33t,623677,1069547520
	2014/08/23 12:01:53.1659607,SI[..C.],\\_SMSvcHostPerfCounters_D.ini:hmx33t,623677,1069547520
	2014/02/18 07:51:58.3286194,SI[MA.B],\\_SMSvcHostPerfCounters_D.ini:Zone.Identifier,623677,23
	2014/08/23 12:01:53.1659607,SI[..C.],\\_SMSvcHostPerfCounters_D.ini:Zone.Identifier,623677,23

Inspect resident files:

	Record: 36112
	[File]  \\BuildDLL.bat
	SI[MACB]: 2007-03-10 21:06:30.0000000   2015-04-11 11:08:03.8517259   2015-04-11 11:08:03.8517259   2014-11-02 20:47:35.2054110
	FN[MACB]: 2007-03-10 21:06:30.0000000   2015-04-11 11:08:03.8517259   2015-04-11 11:08:03.8517259   2014-11-02 20:47:35.2054110
	Reference: 36112:128-1 [Size: 233 bytes|| Size on disk: 0 bytes]
	
mftf -d d -w 36112

	Volume serial number: 1601228289244169585
	Sector size: 512 bytes
	Cluster size: 4096 bytes
	Record size: 1024 bytes
	Starting cluster of the MFT: 786432 [Offset: 0xC0000000]
	000 - 46 49 4C 45 30 00 03 00 ED FB 2F 21 00 00 00 00 FILE0...íû/!....
	010 - 02 00 01 00 38 00 01 00 20 02 00 00 00 04 00 00 ....8... .......
	020 - 00 00 00 00 00 00 00 00 05 00 00 00 10 8D 00 00 ................
	030 - 0B 00 72 20 00 00 00 00 10 00 00 00 60 00 00 00 ..r ........`...
	040 - 00 00 00 00 00 00 00 00 48 00 00 00 18 00 00 00 ........H.......
	050 - 5E 65 1E 3B DE F6 CF 01 00 CF 58 F9 57 63 C7 01 ^e.;_öI..IXùWcÇ.
	060 - 0B E2 DE C7 47 74 D0 01 0B E2 DE C7 47 74 D0 01 .â_ÇGtD..â_ÇGtD.
	070 - 20 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00  ...............
	080 - 00 00 00 00 17 01 00 00 00 00 00 00 00 00 00 00 ................
	090 - B8 58 41 02 00 00 00 00 30 00 00 00 78 00 00 00 ,XA.....0...x...
	0A0 - 00 00 00 00 00 00 04 00 5A 00 00 00 18 00 01 00 ........Z.......
	0B0 - 0F 8D 00 00 00 00 02 00 5E 65 1E 3B DE F6 CF 01 ........^e.;_öI.
	0C0 - 00 CF 58 F9 57 63 C7 01 0B E2 DE C7 47 74 D0 01 .IXùWcÇ..â_ÇGtD.
	0D0 - 0B E2 DE C7 47 74 D0 01 F0 00 00 00 00 00 00 00 .â_ÇGtD.d.......
	0E0 - E9 00 00 00 00 00 00 00 20 00 00 00 00 00 00 00 é....... .......
	0F0 - 0C 03 42 00 75 00 69 00 6C 00 64 00 44 00 4C 00 ..B.u.i.l.d.D.L.
	100 - 4C 00 2E 00 62 00 61 00 74 00 00 00 00 00 00 00 L...b.a.t.......
	110 - 80 00 00 00 08 01 00 00 00 00 18 00 00 00 01 00 ?...............
	120 - E9 00 00 00 18 00 00 00 40 65 63 68 6F 20 6F 66 é.......@echo of
	130 - 66 0D 0A 0D 0A 69 66 20 65 78 69 73 74 20 74 45 f....if exist tE
	140 - 4C 6F 63 6B 2E 6F 62 6A 20 64 65 6C 20 74 45 4C Lock.obj del tEL
	150 - 6F 63 6B 2E 6F 62 6A 0D 0A 69 66 20 65 78 69 73 ock.obj..if exis
	160 - 74 20 74 45 4C 6F 63 6B 2E 64 6C 6C 20 64 65 6C t tELock.dll del
	170 - 20 74 45 4C 6F 63 6B 2E 64 6C 6C 0D 0A 0D 0A 5C  tELock.dll....\
	180 - 54 61 73 6D 5C 62 69 6E 5C 74 61 73 6D 33 32 20 Tasm\bin\tasm32
	190 - 2F 6D 6C 20 2F 6D 20 74 45 4C 6F 63 6B 2E 61 73 /ml /m tELock.as
	1A0 - 6D 0D 0A 5C 54 61 73 6D 5C 62 69 6E 5C 74 6C 69 m..\Tasm\bin\tli
	1B0 - 6E 6B 33 32 20 2F 54 70 64 20 2F 61 61 20 2F 63 nk32 /Tpd /aa /c
	1C0 - 20 2F 56 34 2E 30 20 2F 78 20 74 45 4C 6F 63 6B  /V4.0 /x tELock
	1D0 - 2C 74 45 4C 6F 63 6B 2C 2C 63 3A 5C 74 61 73 6D ,tELock,,c:\tasm
	1E0 - 5C 6C 69 62 5C 69 6D 70 6F 72 74 33 32 2C 74 45 \lib\import32,tE
	1F0 - 4C 6F 63 6B 2E 64 65 66 0D 0A 0D 0A 64 69 0B 00 Lock.def....di..
	200 - 74 45 4C 6F 63 6B 2E 2A 0D 0A 70 61 75 73 65 0D tELock.*..pause.
	210 - 0A 00 00 00 00 00 00 00 FF FF FF FF 82 79 47 11 ........ÿÿÿÿ,yG.
	220 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	230 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	240 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	250 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	260 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	270 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	280 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	290 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	2A0 - FF FF FF FF 82 79 47 11 00 00 00 00 00 00 00 00 ÿÿÿÿ,yG.........
	2B0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	2C0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	2D0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	2E0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	2F0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	300 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	310 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	320 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	330 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	340 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	350 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	360 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	370 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	380 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	390 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	3A0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	3B0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	3C0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	3D0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	3E0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ................
	3F0 - 00 00 00 00 00 00 00 00 00 00 00 00 00 00 0B 00 ................

	----------------------------------------
