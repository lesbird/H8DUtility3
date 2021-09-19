using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Bringing some of Darrell Palen's CMPFile.cs functionality over
// and making it more C# generic to play well with Unity
// 2021 Les Bird

public class H8DCPMFile : MonoBehaviour
{
    /*
    Disk type: byte 5 in sector 0 on H-37 disks (starting from 0) to define disk parameters
    Allocation Block size: number of bytes in an the smallest block used by CP/M on the disk. must be a multiple of 128 (0x80)
            AB numbers start with 0. The directory starts in AB 0.
    Directory Stat: start of directory entries in bytes
    Allocation Block Number Size: number of bytes used in directory entry to reference an allocation block
    Dir Size: number of bytes used for the directory
     0000 =         DPENE	EQU	00000000B
     0040 =         DPEH17	EQU	01000000B
     0060 =         DPEH37	EQU	01100000B
     0008 =         DPE96T	EQU	00001000B
     0004 =         DPEED	EQU	00000100B
     0002 =         DPEDD	EQU	00000010B
     0001 =         DPE2S	EQU	00000001B

    */
    // 0.Disk type, 1.Allocation block size, 2.Directory start, 3.Allocation block byte size, 4.dir size, 5.interleave, 6.Sectors per Track, 7.Sector Size,
    // 8.# Tracks, 9. # heads
    /*
    public int[,] DiskType =
    {
            // 0    1       2     3     4    5  6   7       8  9
            {0x6f, 0x800, 0x2800, 2, 0x2000, 3, 5, 0x400, 160, 2}, // H37 96tpi ED DS
            {0x6b, 0x800, 0x2000, 2, 0x2000, 3, 16, 0x100, 160, 2}, // H37 96tpi DD DS
            {0x67, 0x800, 0x2800, 2, 0x1000, 3, 5, 0x400, 80, 2}, // H37 48tpi ED SS
            {0x62, 0x400, 0x2000, 1, 0x1000, 3, 16, 0x100, 40, 1}, // H37 48tpi DD SS
            {0x63, 0x400, 0x2000, 1, 0x2000, 3, 9, 0x200, 80, 2}, // H37 48tpi DD DS
            {0xE5, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40, 1}, // Default H17 48tpi SD SS
            {0x00, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40, 1}, // Default H17 48tpi SD SS
        };
    */

    public static H8DCPMFile Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    //******************** Build Skew *************************************
    // returns an integer array of size spt with the requested interleave intLv
    // array is in logical to physical format
    // logical sector is array index, value is physical order
    public int[] BuildSkew(int intLv, int spt)
    {
        int physicalS = 0;
        int logicalS = 0;
        int[] count = new int[spt];
        int[] skew = new int[spt];

        // initialize table
        for (var i = 0; i < spt; i++) // initialize skew table
        {
            skew[i] = 32;
            count[i] = i;
        }

        while (logicalS < spt) // build physical to logical skew table
        {
            if (skew[physicalS] > spt) // logical position not yet filled
            {
                skew[physicalS] = (byte)logicalS++;
                physicalS += intLv;
            }
            else
            {
                physicalS++; // bump to next physical position
            }

            if (physicalS >= spt) physicalS = physicalS - spt;
        }

        System.Array.Sort(skew, count); // sort both arrays using skew values and return count array for offset
        return count;
    }
}
