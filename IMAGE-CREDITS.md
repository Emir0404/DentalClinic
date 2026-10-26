# Image credits and licence check (Dental Studio Osmijeh)

All photos come from **Unsplash** and are used under the [Unsplash License](https://unsplash.com/license): free for commercial use, attribution not required.
The licence does **not** include a model release: it does not let you imply that the people shown endorse a product, and it is not a permission to use a person's likeness in a misleading way.

## Status (checked 22 Oct 2026)

- The photographer names are **not recorded** anywhere in this project (only the image IDs in `tools/images.tsv`) and are **not embedded** in the image files, so they could not be recovered automatically. They should be looked up on unsplash.com before the project goes public, and credited here and in the site footer.
- ~~The "doctors" were stock portraits of real people under invented names, and some photos showed children.~~ **Fixed:** those images are now illustrations. Six MEDIUM-risk photos remain (masked staff, a man in profile); they are used as background pictures of treatments, not as people with names.
- Everything on the site is a **demo** (the footer says so), and names, prices and the address are fictional.

## Files

Risk = how identifiable the people are. HIGH: face clearly visible (or a child). MEDIUM: masked, partial or from the side. LOW: mouth/hands only. none: no people.

| File (`wwwroot/img`) | Unsplash image ID | Risk | Note |
|---|---|---|---|
| `hero.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Two adults, faces visible (dentist and patient), used as the home page hero |
| `clinic-bright.jpg` | `photo-1629909613654-28e377c37b09` | none | No people |
| `clinic-chair.jpg` | `photo-1598256989800-fe5f95da9787` | none | No people |
| `waiting-room.jpg` | `photo-1762625570087-6d98fca29531` | none | No people |
| `smile-1.jpg` | `photo-1489278353717-f64c6ee8a4d2` | LOW | Close-up of a smile, partial face |
| `smile-2.jpg` | `photo-1677026010083-78ec7f1b84ed` | LOW | Mouth only |
| `smile-3.jpg` | `photo-1654373535457-383a0a4d00f9` | LOW | Mouth only |
| `smile-4.jpg` | `photo-1769559893692-c6d0623bf8e4` | LOW | Mouth only |
| `exam.jpg` | `photo-1606811971618-4486d14f3f99` | none | No people |
| `xray.jpg` | `photo-1588776814546-1ffcf47267a5` | MEDIUM | Man in profile with glasses, face recognisable |
| `team-work.jpg` | `photo-1588776814546-daab30f310ce` | MEDIUM | Masked staff and patient |
| `consult.jpg` | `photo-1606811841689-23dfddce3e95` | MEDIUM | Two people from behind/side |
| `implant.jpg` | `photo-1593022356769-11f762e25ed9` | none | No people |
| `tools.jpg` | `photo-1771442873038-dda05b6ca447` | none | No people |
| `braces-girl.jpg` | `photo-1598256989809-394fa4f6cd26` | MEDIUM | Patient mouth and part of face |
| `braces-model.jpg` | `photo-1720685193964-4529228a33c1` | none | No people |
| `aligner.jpg` | `photo-1609840114035-3c981b782dfe` | LOW | Mouth and hands |
| `aligner-case.jpg` | `photo-1564420228450-d9a5bc8d6565` | LOW | Hands only |
| `kids.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Child, face clearly visible |
| `kids-2.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Child, face partly visible; masked adult |
| `brushing.jpg` | `photo-1609840113564-ab4aba4956c4` | LOW | Mouth and toothbrush |
| `toothbrush-bamboo.jpg` | `photo-1607613009820-a29f7bb81c04` | none | No people |
| `toothbrush-red.jpg` | `photo-1520013573795-38516d2661e4` | none | No people |
| `veneers.jpg` | `photo-1670250492416-570b5b7343b1` | LOW | Mouth only |
| `cleaning.jpg` | `photo-1681939278218-a755fb2bf2d3` | MEDIUM | Masked woman, eyes visible |
| `extraction.jpg` | `photo-1657470179447-0f5aa16daa91` | MEDIUM | Masked dentist, patient partly visible |
| `doctor-lejla.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Portrait, face clearly visible (used as a fictional doctor) |
| `doctor-tarik.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Portrait, face clearly visible; embroidered text on the jacket (possible real clinic name/logo) |
| `doctor-mia.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Portrait, face clearly visible (used as a fictional doctor) |
| `doctor-adnan.jpg` | *(illustration)* | none | **Replaced 26 Oct 2026** by an original illustration (`tools/generate-illustrations.py`). Was: Portrait, face clearly visible (used as a fictional doctor) |

**Summary (after the replacement):** the 7 HIGH-risk photos are gone. 23 Unsplash photos remain: 6 MEDIUM, 8 LOW, 9 with no people. The home page hero, the two children's pictures and the four doctor portraits are now original illustrations.

## To do before publishing

1. (Still open) Look up and record each photographer (Unsplash photo page) and add a credits line to the footer or an "About the images" page.
2. ~~Replace the HIGH-risk photos~~ Done (see above). The remaining MEDIUM photos could be swapped for no-face photos later; the footer already says the site is a demo.
3. ~~Jacket text on `doctor-tarik.jpg`~~ Gone with the replacement.
