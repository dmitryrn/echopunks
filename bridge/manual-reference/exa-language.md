# EXA language reference

Practical transcription and paraphrase of the Axiom language guide and tutorials in
`C:\GOG Games\EXAPUNKS\Content\manual\digital_en_1.pdf`, PDF pages 4–10
(printed pages 5–17), plus macros from issue 2, PDF page 16 (printed 28–29).
Examples and tables were checked against the page images because PDF text extraction
misreads some zeros, instruction names, and operands. This is a reference, not a
verbatim transcription; the PDFs remain authoritative.

## Model

An EXA is an execution agent running code inside a host on a network. EXAs run
independently and simultaneously. Hosts have limited grid space. Links join hosts;
each end has its own numerical ID, and links can be one-way or inaccessible.
Files contain sequences of numbers and keywords; they have IDs and occupy host
space. Hardware registers are host-specific, may be read-only or write-only, and
are named `#` followed by four characters.

The manual's network diagram shows the player's home host linked to another host,
then to HOST-B containing file 200, then to HOST-C containing `#POWR`. The displayed
link-end IDs illustrate why a link ID is specific to the current host, not a
network-wide route. The tutorial's secret host illustrates a one-way link.

## Operands and registers

* `R`: register; `R/N`: register or integer from -9999 to 9999; `L`: label
  declared by `MARK`. The manual also describes keyword values in registers.
* `X`: general-purpose number or keyword.
* `T`: general-purpose number or keyword; `TEST` overwrites it with 1 or 0.
  `TJMP`/`FJMP` inspect its value, regardless of how it was set.
* `F`: interface to the currently held file. `GRAB` starts the cursor at the
  first value. Reading advances the cursor; writing overwrites the current value
  or appends at end, and advances the cursor.
* `M`: synchronous message interface. A writer waits for a reader and vice
  versa; both numbers and keywords can be transferred. Competing senders or
  receivers are selected unpredictably. Global mode communicates across the
  connected network; local mode only within the host. Different modes cannot
  communicate with one another. `MODE` toggles the setting.

## Value operations

| Syntax | Effect |
| --- | --- |
| `COPY R/N R` | Copy the source to the destination. |
| `ADDI R/N R/N R` | Add the first two operands, store in third. |
| `SUBI R/N R/N R` | Subtract second from first. |
| `MULI R/N R/N R` | Multiply. |
| `DIVI R/N R/N R` | Divide. Division by zero is an error. |
| `MODI R/N R/N R` | Modulo. |
| `SWIZ R/N R/N R` | Reorder/extract digits using a mask. |
| `RAND R/N R/N R` | Random integer between the two bounds, inclusive. |

The SWIZ table prints numbers as four digits. The mask's digit selects an input
position counting from the right; zero produces zero. A negative mask flips
the sign. Examples from the illustrated table:

| Input | Mask | Result |
| ---: | ---: | ---: |
| 6789 | 4321 | 6789 |
| 6789 | 1234 | 9876 |
| 6789 | 3333 | 7777 |
| 6789 | 1211 | 9899 |
| 6789 | -4321 | -6789 |
| -6789 | -4321 | 6789 |
| 6789 | 2000 | 8000 |
| 6789 | 0001 | 0009 |

Math on keywords is a runtime error.

## Branching and tests

| Syntax | Effect |
| --- | --- |
| `MARK L` | Define a label; pseudo-instruction, not executed. |
| `JUMP L` | Jump to label. |
| `TJMP L` | Jump when T is nonzero. |
| `FJMP L` | Jump when T is zero. |
| `TEST R/N = R/N` | Set T to 1 if equal, else 0; `<` and `>` also supported. |
| `TEST EOF` | T is 1 if held file's cursor is at its end, otherwise 0. |
| `TEST MRD` | T is 1 if a message could be read without waiting, otherwise 0. |

The manual's comparison table says numbers compare numerically and keywords
alphabetically; number-versus-keyword comparisons are always false.

## Lifecycle, movement, files, messaging

| Syntax | Effect |
| --- | --- |
| `REPL L` | Copy the EXA; the copy starts at label L. A held file stays with the original and is not copied. |
| `HALT` | Terminate this EXA, dropping any held file. |
| `KILL` | Terminate another EXA in the same host; prioritizes EXAs belonging to the same user, choosing unpredictably among eligible targets. |
| `LINK R/N` | Traverse the specified link from the current host. |
| `HOST R` | Copy the current host name to a register. |
| `MAKE` | Create and grab a new empty file. |
| `GRAB R/N` | Grab a file by ID. |
| `FILE R` | Copy the held file's ID to a register. |
| `SEEK R/N` | Move file cursor relative to current position; clamped to beginning/end. `-9999` and `9999` reliably reach the beginning/end. |
| `VOID F` | Remove the value at the file cursor. |
| `DROP` | Drop the held file. |
| `WIPE` | Delete the held file. |
| `MODE` | Toggle global/local M mode. |
| `VOID M` | Read and discard a message. |
| `NOOP` | Do nothing for one cycle. |
| `NOTE text` | Comment pseudo-instruction. A semicolon also begins a comment anywhere on a line. |

Invalid file access, link traversal, F access without a file, or hardware
register access can terminate an EXA. The manual notes that terminating an EXA
on a failed probe can sometimes be used intentionally.

## Tutorial examples

Move file 200 from inbox to outbox:

```text
LINK 800
GRAB 200
LINK 800
DROP
HALT
```

Read four values, append `(first + second) * third - fourth`, and deliver:

```text
LINK 800
GRAB 200
COPY F X
ADDI X F X
MULI X F X
SUBI X F X
COPY X F
LINK 800
DROP
HALT
```

Write 9999 ten times to a held file:

```text
COPY 0 X
MARK LOOP
COPY 9999 F
ADDI X 1 X
TEST X = 10
FJMP LOOP
```

## Compile-time macros (issue 2)

`@REP N` repeats the lines through `@END` N times before execution. N is a
constant, not a register; repetition blocks cannot be nested. `@{N,M}` substitutes
N on the first repetition and adds M for each successive repetition. These are
compile-time expansion, not runtime loops.

```text
@REP 5
COPY @{1,1} F
@END
```

This expands to `COPY 1 F` through `COPY 5 F`. Substitution can also appear
inside labels, e.g. `TJMP CASE@{0,1}`. Unrolling can reduce cycles at the
expense of expanded code size.
