# Святые противники — исходные запросы генерации

Инструмент: встроенный `image_gen`. Референс стиля: `Assets/Sprits/allEnemy.png`. Оба PNG перенесены в проект без изменения исходных пикселей. Unity уменьшает импортируемую текстуру до 128 px и нарезает кадры; исходный PNG остаётся 1254 × 1254 с прозрачностью. Запрошенный размер 1024 × 1024 генератор не соблюл; нарезка рассчитана по фактическому размеру.

## HolyOrc

Use case: stylized-concept. Production asset: a Unity 2D top-down fantasy pixel-art enemy ANIMATION SPRITE SHEET. The attached allEnemy.png is a STYLE REFERENCE only: match its tiny, readable, dark-outlined, flat-colour RPG sprites, simple bone/wood details, very limited palette and overhead three-quarter view. Create a NEW stocky crimson-red orc with two little ivory tusks, dark leather belt and a substantial wooden club. Red skin signals a holy enemy. Not a skeleton, not a human, no blood. Must be visually legible at 24-32 logical pixels tall.

Layout must be EXACTLY a 1024 by 1024 transparent PNG sheet: FOUR equal 256x256 columns and FOUR equal 256x256 rows, sixteen separate aligned poses of the SAME orc, all facing screen-right. Each cell is a nearest-neighbour 8x enlarged 32x32 pixel-art tile: hard 8-pixel blocks, no subpixel details or smooth shading. Keep the character/club completely inside every cell with generous transparent padding, identical scale, foot baseline at y=224 within each tile and consistent centre at x=128. No grid, no guides, no captions, no text.

Row 1: four subtle idle/breathing frames.
Row 2: four clearly distinct walking frames, alternating legs and swinging club, coherent loop.
Row 3: four club-swing attack frames: ready, overhead wind-up, downward strike with a tiny ivory-red arc, recover.
Row 4: four magical defeat frames: recoil, squash into a few red/ivory pixels, scattered red sparks, a small fading spark. NO corpse, NO blood, NO gore.

Genuinely transparent background. Flat crisp pixel clusters with dark charcoal outline, burgundy shadows and warm crimson highlights. No ground tile, no scenery, no shadows extending beyond the tile, no lighting gradients or antialiasing. This is a game-ready sprite atlas, not a character design presentation.

## HolyGhost

Use case: stylized-concept. Production asset: a Unity 2D top-down fantasy pixel-art enemy ANIMATION SPRITE SHEET. The attached allEnemy.png is a STYLE REFERENCE only: match its tiny, readable, dark-outlined, flat-colour RPG sprites, very limited palette and overhead three-quarter view. Create a NEW floating holy ghost: hollow ivory hood/face with two dark eyes, crimson-red spectral robe and two clear trailing flame-like tails, a small hovering red spark. The red silhouette signals a holy enemy and should be clearly different from slimes or skeletons. Cute-menacing fantasy, no blood. Must be readable at 24-32 logical pixels tall.

Layout must be EXACTLY a 1024 by 1024 transparent PNG sheet: FOUR equal 256x256 columns and FOUR equal 256x256 rows, sixteen separate aligned poses of the SAME ghost, all facing screen-right. Each cell is a nearest-neighbour 8x enlarged 32x32 pixel-art tile: hard 8-pixel blocks, no subpixel details or smooth shading. Keep the whole ghost inside every cell with generous transparent padding, identical scale and a consistent visual centre at x=128, y=128 within each tile. No grid, guides, captions or text.

Row 1: four hovering idle frames with subtle bob and waving tails.
Row 2: four clearly different gliding frames, tails streaming backward, coherent loop.
Row 3: four magic attack frames: gather little red spark, open sleeves, release a compact red-ivory orb toward screen-right, recover. Do not include a detached projectile far from the character.
Row 4: four magical defeat frames: recoil, contracting crimson wisp, scattered small red and ivory sparks, tiny fading spark. NO body or corpse, NO blood or gore.

Genuinely transparent background. Crisp flat pixel clusters, dark burgundy outline, crimson and warm red cloth with an ivory face. No ground tile, scenery, backdrop, smooth transparency gradients, glows or antialiasing. This is a game-ready sprite atlas, not a design presentation.
