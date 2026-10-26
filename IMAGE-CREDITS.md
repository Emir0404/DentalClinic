# Image credits and licence check (Dental Studio Osmijeh)

## Summary (26 Oct 2026)

**No picture on the site shows a person**, not even a hand or a mouth.

- **23 pictures are original illustrations** drawn from simple shapes by `tools/generate-illustrations.py`: the home page hero,
  the four doctors, the two children's pictures, the team and consultation pictures, all smiles, braces, aligner, veneers,
  x-ray, instruments, cleaning, extraction and brushing. They belong to this project, so there are no licences or model releases to worry about,
  and nobody is shown under a name that is not theirs.
- **7 photos remain, all without any people** (rooms and objects), from **Unsplash**, used under the
  [Unsplash License](https://unsplash.com/license): free for commercial use, attribution not required.
  Photographer names are not recorded in this project (only the image IDs in `tools/images.tsv`); crediting them is a courtesy still to do.
- Everything on the site is a **demo** (the footer says so): names, prices and the address are fictional.

## The 7 remaining photos (`wwwroot/img`)

| File | Unsplash image ID | What it shows |
|---|---|---|
| `clinic-bright.jpg` | `photo-1629909613654-28e377c37b09` | Empty treatment room |
| `clinic-chair.jpg` | `photo-1598256989800-fe5f95da9787` | Dental chair in an empty room |
| `waiting-room.jpg` | `photo-1762625570087-6d98fca29531` | Empty waiting room |
| `implant.jpg` | `photo-1593022356769-11f762e25ed9` | Teaching model of an implant |
| `braces-model.jpg` | `photo-1720685193964-4529228a33c1` | Plastic jaw models with braces |
| `toothbrush-bamboo.jpg` | `photo-1607613009820-a29f7bb81c04` | Bamboo toothbrushes in a glass |
| `toothbrush-red.jpg` | `photo-1520013573795-38516d2661e4` | A toothbrush on a blue background |

A test (`PhotoPolicyTests`) fails if a photo that is not on this list is added to `tools/images.tsv`, so a picture of a person
cannot slip back in unnoticed.

## History

The first version used 30 stock photos, 7 of which showed identifiable people (four portraits used as "doctors", two photos of children,
and the hero) and 9 more showed parts of people (faces in profile, masked staff, mouths, hands). All 23 were replaced by illustrations.
