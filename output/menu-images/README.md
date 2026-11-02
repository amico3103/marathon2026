# Generated menu images

Generated using the built-in image_gen tool. Six separate square images, visually inspected.

Shared prompt: Photorealistic professional restaurant menu photography. One white ceramic plate centered on a warm light ivory tabletop, slightly overhead view, soft natural daylight from upper left, subtle shadows. Food and plate centered for circular thumbnail cropping. No text, logos, watermark, collage, cutlery or extra plates.

Subject prompts and files:

- `green-salad.png`: mixed green leaves, cherry tomatoes, cucumber, light vinaigrette.
- `grilled-salmon.png`: golden seared salmon fillet, lemon butter sauce, seasonal vegetables.
- `truffle-mushroom-risotto.png`: creamy arborio rice, wild mushrooms, parmesan and truffle.
- `margherita-pizza.png`: whole artisan pizza, tomato sauce, mozzarella, fresh basil.
- `fresh-avocado-toast.png`: two sourdough slices, sliced avocado, microgreens and cherry tomatoes.
- `grilled-salmon-2.png`: salmon with grill marks, lemon butter sauce, asparagus and roasted carrots, distinct plating.

These are AI-generated illustrations of the dishes, not photographs of actual restaurant servings.

Published to `s3://restaurant-backend-dev-images-215493805484/assets/img/dishes/generated-v1/`.
All eight matching DynamoDB Dishes records reference the six images through `imageUrl`.
Same-name dishes in different locations share an image; Grilled Salmon 2 uses its own variant.
The group sessions API includes `imageUrl` (Lambda version 11).
Previous dish records are backed up at `/home/amico/marathon-dish-images-backup-03371kpt/dishes.json`.
