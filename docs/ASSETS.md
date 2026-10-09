# Total Fog artwork

Original Real Fog of War gameplay textures and assets retain their upstream
Apache 2.0 attribution in NOTICE.md. The portable texture folder is loaded for
the current supported game version as well as the historical versions.
The active build loads ordinary PNGs from `LegacyAssets/Textures/`. It has no
custom AssetBundle or shader. The removed upstream bundle contained only
Texture2D assets duplicated by those PNG paths.

The public `upstream-baseline` tag preserves the original payload for comparison.
Baseline staging restores its AssetBundle and original load folders, then adds
the same `LegacyAssets` portable texture fallback used by the candidate. The
original DLL and definition bindings stay unchanged. Normal
staging removes any stale `Assets/` folder. Historical version payloads remain
frozen. Upstream scratch projects, failure experiments, duplicate translation
templates/ZIP and superseded branding binaries were removed from `Originals/`
on 7 October; they remain recoverable in Git history, with attribution retained.

## Active branding

Andreas supplied the active preview and icon on 2026-10-04 as
`Total Fog Preview.png` and `Total Fog Mod Icon.png`. Both are copied without
editing or resampling.

- `About/Preview.png`: 640 by 360. SHA-256
  `a49e812304cd4f4021d82aeec853a45ab60962cc857d739487714da9e0c997ac`.
- `About/ModIcon.png`: 64 by 64. SHA-256
  `6394dc8c108fc4de91d1ae9ae6a6ac4c9566deea5a4099ccb945cc3f64f6325b`.

The manifest records the supplied preview's actual height. The shared validator's
640 by 358 recommendation remains a documented warning for this user-supplied
image; it does not require altering the provided artwork.

## Historical branding prompts

The previous branding images were generated with OpenAI image generation on
2026-10-04. Their exact prompts remain below, and the superseded images remain
recoverable in Git history. Neither is part of the current payload.

### Previous preview

Source: `preview.png`. Center-cropped to 1672 by 935, then resampled without
stretching to `About/Preview.png`, 640 by 358.

Exact prompt:

Create a 16:9 landscape illustration for a RimWorld fog-of-war mod called Total Fog. No text, no title, no logos, no UI, no watermark. A small isolated science-fiction frontier colony viewed from above at a gentle angle, warm amber lights around a few modest wooden and metal buildings and tiny human silhouettes. The colony occupies the lower central third. Dense layered charcoal and blue-grey fog conceals most of the surrounding landscape; hints of unknown creature silhouettes are barely suggested inside distant mist, not fully visible. Graphic painterly game-key-art style, restrained colors, crisp warm colony light against cool mist, elegant composition with generous dark fog around the settlement. Must remain legible when cropped to 640 by 358 pixels.

### Previous icon

Source: `icon.png`. Resampled to `About/ModIcon.png`, 64 by 64.

Exact prompt:

Generate a square icon for Total Fog, a science-fiction colony fog-of-war mod. No text, no letters, no UI, no logos, no watermark. A single simple warm amber lantern or watch-light at the center of a charcoal dark square, illuminating a tiny angular island of ground, surrounded by bold layered cool blue-grey fog shapes that conceal the outer corners. Stylized flat painterly graphic symbol with a strong silhouette and very few details. High contrast, readable at 64 by 64 pixels, generous margin, restrained orange and slate palette matching a remote frontier colony surrounded by mist. The main shape should fill most of the square and remain unmistakable at tiny size.
