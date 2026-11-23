# Trash World News issue 2 — page-by-page notes

Source: `C:\GOG Games\EXAPUNKS\Content\manual\digital_en_2.pdf`. Numbers are
PDF page numbers. These are visual and technical notes, not a complete
word-for-word transcription. Technical sections may spoil later puzzles.

1. Cover: November 1997, “The Games Issue,” TEC Redshift illustration.
2. Credits and contents: hacker battles, Redshift, King's Ransom Online,
   WonderDisc, modem control, GIS, satellites, credit cards and macros.
3. Printed 2–3: editor's introduction.
4. Printed 4–5: hacker battles run opposing EXAs simultaneously; scoring
   varies by network. Battles have cycle and storage limits; exceeding the
   storage limit stalls an EXA until space is available. An arena is pictured.
5. Printed 6–7: Redshift advertisement and introduction.
6. Printed 8–9: Redshift `WAIT` pauses until next frame (30 Hz); `DATA` defines
   initial held-file numeric values and is not executed. Display is 120×100;
   each EXA has a 10×10 sprite positioned by `GX`, `GY`, with depth `GZ`
   (-9 to 9). `GP` accepts three digits: operation (0 off, 1 on, 2 toggle),
   x coordinate, y coordinate. Illustration: `190` turns on x=9,y=0;
   `209` toggles x=0,y=9.
7. Printed 10–11: writing 300+character code to `GP` sets a built-in font
   sprite. Character codes match issue 1's highway sign table. White-pixel
   overlap causes each EXA's `CI` to take the other's `CO`; highest CO wins
   if multiple overlap, and `GZ` does not affect collision. Diagram shows
   nonoverlapping and overlapping shapes, with -9999 in CI when no collision.
   Audio: `#SQR0`, `#SQR1`, `#TRI0`, `#NSE0`; 0 stops audio. Input: `#PADX`
   and `#PADY` are -1/0/1; `#PADB` stores X,Y,Z,start as ones/tens/hundreds/
   thousands digits; `#EN3D` is 0/1. Piano diagram labels notes 36–96.
8. Printed 12–13: watch advertisement; King's Ransom Online buildings contain
   `(name, type, owner player ID, zero or more sub-building file IDs)` with
   potentially nested buildings. Weapons contain `(name, damage, damage type,
   speed, weight, durability, optional effects)`; durability 999 is perfect,
   0 broken.
9. Printed 14–15: WonderDisc: region strings `SSEC`, `SSEA`, `SSEE`; read
   `#TRAK`, copy the requested track and replace its region code, drop the
   copy into buffer for the other EXA. Illustrated drive unlock key:
   `8, 0, 3, 2, 7, 1, 0, 4, 9, 5, 1, 2, 5, 2, 6`.
   The PDF's extracted text misreads the zeros; this sequence is from the image.
10. Printed 16–17: letters, fictional network tips and advertisements. These
    include fictional telephone numbers and may point to optional content.
11. Printed 18–19: modem: write one digit at a time to `#DIAL`; once connected,
    link 800 reaches the remote host; writing -1 hangs up. Diagram shows
    a modem register connected to multiple hosts.
12. Printed 20–21: StreetSmarts GIS: each city block is a host; north=800,
    east=801, south=802, west=803. GLOBALYST represents countries as hosts,
    linked according to physical routes. Two illustrated network maps.
13. Printed 22–23: satellite dish motors control azimuth/elevation; write 1
    or -1 to move. Payload encryption adds successive key values to data
    modulo 10000, repeating key when exhausted. Example 7407+3894 = 1301.
    Diagram shows broadcast from providers via satellite to viewers.
14. Printed 24–25: Haxtasy hacker-club advertisement.
15. Printed 26–27: 16-digit card checksum: double digits at odd positions
    counted from the left, subtract 9 when a doubled value exceeds 9; add all
    digits and check whether sum is divisible by 10. Diagram's example sum is
    70. A checksum-valid number does not establish an actual account.
16. Printed 28–29: loop unrolling and compile-time `@REP`, `@END`, `@{N,M}`
    macros. See `exa-language.md` for syntax and example.
17. Printed 30–31: fictional kitchen column about instant ramen.
18–19. Printed 32–35: part 2 of “Passing Resemblance,” fiction.
20. Printed 36 and inside back cover: three nonogram grids, their row/column
    clues and instructions; picture puzzles rather than EXA language reference.
21. Back cover: console and cartridge artwork.
