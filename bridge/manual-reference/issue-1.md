# Trash World News issue 1 — page-by-page notes

Source: `C:\GOG Games\EXAPUNKS\Content\manual\digital_en_1.pdf`. Numbers below
are PDF page numbers; most PDF pages are two printed pages. These are visual and
technical notes, not a complete word-for-word transcription. The original
contains fiction, advertisements, and artwork as well as game documentation.
Some topics may reveal information used in later levels.

1. Cover: October 1997, “The EXA Issue,” robot EXA illustration.
2. Credits and contents: tutorial, Axiom reference, phage, highway signs,
   DigiCash, legacy storage, WorkHouse, Equity First, kitchen and fiction.
3. Printed 2–3: editor's manifesto and introduction to EXAs and field reports.
4. Printed 4–5: computer advertisement; EXAs, code, registers; illustrated
   editor with code and X/T/F/M. See `exa-language.md`.
5. Printed 6–7: diagram of hosts, numbered links, a file, and `#POWR`; hosts
   have limited grid spaces, links may be one-way, files hold values, hardware
   registers interact with attached devices.
6. Printed 8–9: first two tutorials and complete example programs; file cursor
   advances on F access. See `exa-language.md`.
7. Printed 10–11: one-way-link messaging tutorial and loop tutorial. Global
   and local M modes are distinct; senders/receivers wait for each other.
8. Printed 12–13: runtime errors, including invalid access and divide-by-zero;
   failed file/link probes can be deliberately used.
9. Printed 14–15: EXA language guide: operand notation, X/T/F/M, hardware,
   arithmetic, swizzle table, branches. See `exa-language.md`.
10. Printed 16–17: tests, lifecycle, movement, messaging, files, and miscellaneous
    instructions. See `exa-language.md`.
11. Printed 18–19: fictional phage background and experiment connecting
    infected tissue as EXA hosts.
12. Printed 20–21: phage nodes connected by links; illustrated hosts have `#NERV`
    registers. Article describes resting potential around -70 mV, spikes around
    50 mV, suppressed potential around -120 mV; facing page is a surplus ad.
13. Printed 22–23: highway sign. Write triplets `(row, column, character)` to
    `#DATA`, with row and column starting at 0; writing to `#CLRS` clears the
    sign. Character mapping: 0 blank; 1–26 A–Z; 27–36 digits 0–9; 37 `.`;
    38 `?`; 39 `!`. Picture shows a sign reading “ISLANDMIKEY / WUZ HERE”.
14. Printed 24–25: DigiCash; customer database usually file 200 has repeating
    `(customer ID, dollars owed, cents owed)` triples; transaction log usually
    file 201 has `(date, customer ID, dollars paid, cents paid)` quadruples.
    Diagram shows clock registers and washer/dryer hosts attached to the network.
15. Printed 26–27: tape backup data is concatenated at the beginning; metadata
    at end consists of `(name, offset, length)` triples. Example `SECRET, 3, 4`
    means read four values starting at offset 3. Drive arrays have files 200–209,
    each 100 values split into ten chunks of ten. Three-digit chunk address:
    drive, file offset from 200, chunk number; `527` means drive 5, file 202,
    offset 70, read 10 values. File 200 of first drive contains an entry index.
16. Printed 28–29: WorkHouse user host: one file per account, including name,
    birth date and daily earnings. Jobs host contains active jobs; tasks come
    from partners. Diagram depicts connected user/job databases.
17. Printed 30–31: Equity First account files begin `(account number, name,
    type, *)`; type 2 checking, 7 loan. Then transaction quadruples
    `(other account, CREDIT/DEBIT, dollars, cents)`; credits and debits must
    match. Illustrated $33.50 transfer uses reciprocal entries. ATM `#CASH`
    counts $20 bills; writing 20 to `#DISP` requests one.
18. Printed 32–33: fictional kitchen column about reheating donuts; not EXA
    programming documentation.
19–20. Printed 34–36: part 1 of the fiction “Passing Resemblance”; decorative
    art and quotation. No EXA instruction reference.
21. Back cover: repeated EXA illustrations.
