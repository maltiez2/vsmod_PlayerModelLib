# Player Model Lib

This is a library for adding new player models. It does not add new model on its own. Players can choose a model they want in the character-creation window. All players will see the model they choose. This mod is required both on client and server.
On its own, allows players to choose their model size.
Use Config lib to change default model parameters.
Please report bugs via official VS discord server. Comments on moddb have high chance to be ignored.

---

Replaces character selection dialog. Adds scroll areas to skin parts and class selection tabs.
Replaces armor textures with ones that dont hide face inside helmets.

Adds two commands, that allow for specified players to be able to select specified player model even if it is disabled:
```
/player <player_name> enablePlayerModel <domain:code>
/player <player_name> disablePlayerModel <domain:code>
```

Mods that change default seraph model skin parts might be incompatible with this library (but some of them will still work).

Mods that change the default seraph model itself will most probably be incompatible.

### Some mods that use this library:
- Female Seraph
- Lizardfolk Playermodel
- Beasts of Men
- Kobold Player Model
- Racial Equality
- Vintage Goat Player Model
- Birdplayer
- Vintage Birbs (Avali Mod)
- Skeletons
- Fluffy Dreg Player Model
- Experimental Kobold Player
- Humans
- Insectoid Player Model
- Skaven/Rat Player Model
- Robo-Seraph Playermodel
- Lupines
- Arachnoids
- Raccoon Player Model

### Known issues:

- additional hairstyles from lore location dont work
- Thanks to Kathanon for scroll functionality for skin parts, it is integrated into the lib, no need in Scroll in Character Creation mod if you have this library installed.

### For modders:

To add your own model to be used with this library, make a content mod that depends on this library. Make the model, skin parts, textures. Then create 'config' folder inside your domain folder, and in it create 'customplayermodels' folder. Add JSON file with configuration of your model to this folder.

This library contains vanilla facial expressions adjusted to support new way of defining eye color. If you need vanilla facial expressions, use ones provided by library. Example is in API documentation.

You can add a list of classes that a specific model can choose, so it is possible to make a races mod with different classes for each model. If you specify no classes, all classes will be available for this model.

You can add a list of classes that won't be available for your model.

You can add a list of additional traits that player will have on top of class traits.

You can specify new clothing/armor models to be used with your model, but these models should use the same textures as the original models.

The library will automatically add all missing animation from seraph model (include modded), so you only need to make animations you want to replace. But if your model is too different from seraph model you might want to rework all animations.

Also, automatically add missing attachment points from vanilla seraph model. But for that to work, you should have same names for shape elements in your custom model as in the vanilla one.

If CollisionBox is specified, player collision box will be changed to specified values. Default vanilla values are [0.6,1.85].

If EyeHeight is specified, it will be used instead of the vanilla value of 1.7.

SizeRange defines limits to model size slider, by default it is from 80% to 120%.

Shape paths in wearable shape replacements support `{variant}` construction, it will be replaced with corresponding item variant value.

Use `"overlayTexture": true` for texture skin parts that modify model main shape textures!

Supports new format for character attributes lang entries (uses string.Format under hood):
```
"game:charattribute-damageReceivedFactor": "{0:P0} all damage received",
```



Texture skin parts always have to specify textureTarget, and specify targetSkinParts if they need to replace textures in shape skin parts (otherwise it will try to replace textures in player model). To add texture on top of existing one (like seraph underwear) you need to specify overlayTexture: true. Because of these changes, vanilla approach to change eyes color does not work anymore, you either need to use models and textures for facial expressions provided by library, or make your own overlay textures for vanilla eye colors.




New player stats:

```
sprintSpeed - multiplies player walk speed when sprinting
sneakSpeed - multiplies player walk speed when crouching
backwardSpeed - multiplies player walk speed when moving backwards
swimSpeed - multiplies player movement speed when swimming
warmthBonus - adds this amount of degrees to player warmth bonus from clothes
nutritionFactor - multiplies amount of nutrition player gets from any food
damageReceivedFactor - multiplies all received damage, except from damage of "Heal" type, addative with other received damage/healing factors added by this library
healingReceivedFactor - multiplies received damage of "Heal" type, addative with other received damage/healing factors added by this library
maxSaturationFactor - multiplies max saturation, how it interacts with other mods depends on other mods implementation
buoyancyFactor - multiplies amount of motion received from bouyanci emulation
canSwim - if less then zero (default value is 0), then player cant swim, combine it with buoyancy factor of -1
temporalStabilityDropRate - multiplies amount of temporal stability removed from player before other stats applied (do not affect amount of stability added to player)
temporalStabilityOffset - constant amount of stability added/removed to/from player per second in some units (experiment to determine the valye you want), applied after all multiplier stats
temporalStabilityRecoveryRate - multiplies amount of temporal stability added to player before other stats applied (do not affect amount of stability removed from player)
temporalStabilityEffectDirection - multiplies amount of temporal stability added/removed to/from player by -1/0/1 depening on its value (<0/=0/>0), applied before rate stats, equal to 1 by default
temporalStabilityCaveDropRate - multiplies amount of temporal stability removed from player when is in a cave or a dark room (max sun light level < 8)
temporalStabilitySurfaceDropRate - multiplies amount of temporal stability removed from player when is outside a cave a dark room (max sun light level > 7)
temporalStabilityCaveOffset - constant amount of stability added/removed to/from player per second in some units (experiment to determine the valye you want) when is in a cave or a dark room (max sun light level < 8), applied after all multiplier stats
temporalStabilitySurfaceOffset - constant amount of stability added/removed to/from player per second in some units (experiment to determine the valye you want) when is outside a cave a dark room (max sun light level > 7), applied after all multiplier stats
nightWalkSpeed
dayWalkSpeed
nightDamageFactor
dayDamageFactor
nightHealingFactor
dayHealingFactor
darknessWalkSpeed
lightWalkSpeed
darknessDamageFactor
lightDamageFactor
darknessHealingFactor
lightHealingFactor
saturationLossFactor
breathType
canBreathInLight
canBreathInDarkness
canBreathInCaves
canBreathOnSurface
fruitNutritionFactor - multiplies amount nutriotion of fruit food category that player gets
vegetableNutritionFactor - multiplies amount nutriotion of vegetable food category that player gets
proteinNutritionFactor - multiplies amount nutriotion of protein food category that player gets
grainNutritionFactor - multiplies amount nutriotion of grain food category that player gets
dairyNutritionFactor - multiplies amount nutriotion of dairy food category that player gets
gravityDamageFactor - multiplies received damage of "Gravity" type, addative with other received damage/healing factors added by this library
fireDamageFactor - multiplies received damage of "Fire" type, addative with other received damage/healing factors added by this library
bluntDamageFactor - multiplies received damage of "BluntAttack" type, addative with other received damage/healing factors added by this library
slashingDamageFactor - multiplies received damage of "SlashingAttack" type, addative with other received damage/healing factors added by this library
piercingDamageFactor - multiplies received damage of "PiercingAttack" type, addative with other received damage/healing factors added by this library
suffocationDamageFactor - multiplies received damage of "Suffocation" type, addative with other received damage/healing factors added by this library
healDamageFactor - multiplies received damage of "Heal" type, addative with other received damage/healing factors added by this library
poisonDamageFactor - multiplies received damage of "Poison" type, addative with other received damage/healing factors added by this library
hungerDamageFactor - multiplies received damage of "Hunger" type, addative with other received damage/healing factors added by this library
crushingDamageFactor - multiplies received damage of "Crushing" type, addative with other received damage/healing factors added by this library
frostDamageFactor - multiplies received damage of "Frost" type, addative with other received damage/healing factors added by this library
electricityDamageFactor - multiplies received damage of "Electricity" type, addative with other received damage/healing factors added by this library
heatDamageFactor - multiplies received damage of "Heat" type, addative with other received damage/healing factors added by this library
injuryDamageFactor - multiplies received damage of "Injury" type, addative with other received damage/healing factors added by this library
acidDamageFactor - multiplies received damage of "Acid" type, addative with other received damage/healing factors added by this library
```



Example of model configuration file:
```
{
    "kobold": {
        "ShapePath": "kobold:entity/humanoid/seraph-faceless",
        "AvailableClasses": ["commoner", "hunter"],
        "SkipClasses": ["commoner"],
        "ExtraTraits": ["precise"],
        "CollisionBox": [ 0.6, 1.85 ],
        "EyeHeight": 1.7,
        "SizeRange": [ 0.8, 1.2 ],
        "WearableModelReplacers": {
            "game:armor-legs-plate-*": "kobold:entity/humanoid/seraph/armor/plate/legs"
        },
        "WearableModelReplacersByShape": {
            "game:entity/humanoid/seraph/armor/plate/legs": "kobold:entity/humanoid/seraph/armor/plate/legs"
        },
        "SkinnableParts": [
            {
                "code": "baseskin",
                "type": "texture", 
                "textureTarget": "seraph",
                "overlayTexture": true,
                "overlayMode": "Normal",
                "variants": [
                    { "code": "skin1", "texture": "kobold:entity/humanoid/seraphskinparts/body/skin1" },
                    { "code": "skin2", "texture": "kobold:entity/humanoid/seraphskinparts/body/skin2" },
                    { "code": "skin3", "texture": "kobold:entity/humanoid/seraphskinparts/body/skin3" },
                    { "code": "skin4", "texture": "kobold:entity/humanoid/seraphskinparts/body/skin4" },
                    { "code": "skin5", "texture": "kobold:entity/humanoid/seraphskinparts/body/skin5" }
                ]
            }
        ]
    }
}
```
JSON API documentation:
```
{
    "serpah": {
        "ShapePath": "yourdomain:entity/humanoid/seraph-faceless", // Path to shape file of your custom model
        "BaseShapeCode": "playermodellib:seraph", // Will use this base shape to auto adjust clothes and armor shapes, though you need to use same shape structure as base shape (you find those in the lib assets folder), currently available: "seraph" and "digitigrade" (WIP)
        "Group": "temporal", // model will be put into this group. If not specified, model will have its own group. Domain is not appended to the group, so you can specify groups existing in other mods
        "Icon": "mydomain:icons/seraph", // path to icon in textures, should be square, ideally 32x32 pixels
        "GroupIcon": "domain:icons/temporal", // path to group icon, expected resolution is 32x44. You only need to specify once, but you can do it multiple times, what icon will be used depends on mod load order and not defined
        "SkinnableParts": [ // Skin parts, works in simmilar way to vanilla, except for textures
            {
                "code": "baseskin", // Unique code of a skin part
                "type": "texture",  // Skin part type, possible values: Shape, Texture, Voice
                "textureTarget": "girlbase", // Texture code inside shape that should be overriden/overlayed
                "overlayTexture": true, // If set to false, texture will be replaced, otherwise it will be overlayed. False by default.
                "overlayMode": "Normal", // Overlay mode, possible values: Normal, Darken, Lighten, Multiply, Screen, ColorDodge, ColorBurn, Overlay, OverlayCutout
                "variants": [ // List of options for this skin part
                    { "code": "chocolate", "texture": "yourdomain:bodies/boy/chocolate" },
                    { "code": "caramel", "texture": "yourdomain:bodies/boy/caramel" }
                ]
            },
            {
               "code": "breasts",
               "type": "shape",
               "shapeTemplate": { "base": "breasts/breasts-{code}" },
               "disableElementsByVariantCode": { // Disables shape elements of base shape, skin parts and wearable items when corresponding skin part variant is selected
                  "none": ["Breasts", "*Breasts-armor"] // '*' at the beginning of element name will bypass clothes and armor prefixes
            },
              "variants": [
                    { "code": "none"},
                    { "code": "s" },
                    { "code": "m" },
                    { "code": "l" }
                ]
            },
            {
                "code": "facialexpression",
                "type": "shape",
                "variants": [
                    { "code": "angry", "shape": { "base": "playermodellib:seraphfaces/angry" } },
                    { "code": "grin", "shape": { "base": "playermodellib:seraphfaces/grin" } }
                ]
            },
            {
                "code": "eyecolor",
                "type": "texture", 
                "textureTarget": "playermodellib-iris",
                "targetSkinParts": ["facialexpression"], // If not specified or left empty this skin part will target textures inside main model. Otherwise it will target only textures inside models of skin parts specified here. Add "base" to this list to also target main model.
                "variants": [
                    { "code": "acid-green", "texture": "playermodellib:eyes/acid-green", "color": 000255000 }, // Color field will override auto generated color. Set it in format 'RRRGGGBBB' where 'RRR' is red channel value from 0 to 255, same for 'GGG' and 'BBB'.
                    { "code": "aquamarine", "texture": "playermodellib:eyes/aquamarine" },
                ]
            },
            {
                "code": "subspecies",
                "type": "shape",
                "useDropDown": true,
                "targetSkinParts": ["furcolor"], // if specified in shape skin part, texture skin parts from this list will also target this shape skin part
                "shapeTemplate": { "base": "beastsofmen:animalextras/canine/{code}" },
                "variants": [
                    { "code": "canineearsbeta" },
                    { "code": "foxinearsbeta" }
                ]
            },        
            {
                "code": "furcolor",
                "type": "texture",
                "useDropDown": true,
                "textureTarget": "baseears",
                "textureTemplate": "beastsofmen:racials/canine/{code}",
                "variants": [
                    { "code": "arctic" },
                    { "code": "forest fire" }
                ]
            }
        ],
        "WearableModelReplacers": { // Will replace shapes of wearable items when this model is in use. Specify item code (supports wildacrds) and path to shape.
            "game:armor-body-plate-*": "yourdomain:clothes/armor/plate/body",
            "game:armor-body-scale-*": "yourdomain:clothes/armor/scale/body"
        },
        "WearableCompositeModelReplacers": { // Will replace shapes of wearable items when this model is in use. Specify item code (supports wildacrds) and shape in shape  CompositeSHape format.
            "game:armor-body-*-*": { "base": "yourdomain:clothes/armor/{construction}/body", "overlays": [ { "base": "game:entity/humanoid/seraph/clothing/shoulder/collar-cape-hood-fancy" }]  },
        },
        "WearableModelReplacersByShape": { // Will replace shapes of wearable items (and shape overlays) when this model is in use. Specify path to origin shape and to new shape.
            "game:entity/humanoid/seraph/clothing/upperbody/shortsleeve": "yourdomain:clothes/clothing/upperbody/shortsleeve",
            "game:entity/humanoid/seraph/clothing/upperbody/longsleeve": "yourdomain:clothes/clothing/upperbody/longsleeve",
        },
        "AvailableClasses": ["commoner", "hunter"], // Only these classes will be available for this model. If not specified all classes are available (except for classes from SkipClasses).
        "SkipClasses": ["malefactor"], // These character classes will not be avaialbe for this model.
        "ExtraTraits": ["fleetfooted"], // Extra traits on top of classes that player will have when using this model
        "ExclusiveClasses": ["smith"], // Classes specified here will be available only to this model and other model that add them to this list
        "CollisionBox": [ 0.6, 1.85 ], // If specified will override collision and selection boxes
        "EyeHeight": 1.7, // If specified will override eye height.
        "SizeRange": [ 0.8, 1.2 ], // Will limit how far player can change model size using slider in character creation window.
        "ScaleColliderWithSizeHorizontally": true, // If set to true player collision box width will be scaled with model size.
        "ScaleColliderWithSizeVertically": true, // If set to true player collision box height will be scaled with model size.
        "MaxCollisionBox": [ 1.0, 1.99 ], // Will prevent collision box to extend past this size when scaled with model size.
        "MinCollisionBox": [ 0.1, 0.1 ], // Will prevent collision box to shrink past this size when scaled with model size,
        "MaxEyeHeight":  1.95, // Upper limit to eye height when scaled with model size
        "MinEyeHeight":  1.0, // Lower limit to eye height when scaled with model size
        "AddTags": ["animal"], // Additional entity tags, dont add dynamic tags (like "state-onground")
        "RemoveTags": ["seraph"], // Tags that will be removed from entity, dont remove dynamic tags (like "state-onground")
        "ModelSizeFactor": 0.9, // Additional scaling factor for model,
        "HeadBobbingScale": 1.0, // Scales head bobbing amplitude, use it to reduce head bobbing for small models
        "GuiModelScale": 1.0, // Resizes model when displayed in GUI,
        "Enabled": true, // If set to 'false', this model will not appear in list of models in character creation dialog,
        "WalkEyeHeightMultiplier": 0.9, // Eye and collier height modifier when walking forwards or sideways
        "SprintEyeHeightMultiplier": 0.8, // Eye and collier height modifier when sprinting forwards or sideways
        "SneakEyeHeightMultiplier": 0.6, // Replaces vanilla sneak eye and collier height modifier
        "StepHeight": 1.2 // Default step height. It rescales step height for player, instead of replacing (you still need to specify desired step height, not scaling factor), so if other mods change step height correctly, such change will be taken into account.
    }
}
```



If you want to make your models be compatible with vanilla and modded animations there are two ways you can choose:

1) You can make your model key body parts to be exactly the same size and position (also names) as seraph model.
For standard animation you want torso and arms to be the same for the majority of animations to be more compatible in first person
Also for standard animations you want also legs and neck to be the same for third person animations
For Combat Overhaul animations torso and arms is sufficient both for fp and tp

2) You can replace animations with your own.
For standard animation you just add them to your model shape file with same codes as ones you want to replace. All missing animations will be added from seraph shape
For Combat Overhaul you want to add your own animations with your model code at the end of animation name. Ask me in discord for more details.

When using base shape functionality you can export adjusted clothes and armor shapes. You need to turn this feature on via Config lib in Player Model lib config.

If you want to add new clothes/armor models outside of model mod you dont need to patch anything, just put a file named 'model-replacements-bycode.json' in config folder in your assets and specify in it the models like that. First specfiy model code with its domain, then items and models to replace with.
```
{
  "kobold:kobold": {
    "game:armor-legs-plate-*": "kobold:entity/humanoid/seraph/armor/plate/legs"
  } 
}
```

You can also do it with whole compositeShapes, file is 'composite-model-replacements-bycode.json'
```
{
  "kobold:kobold": {
    "game:armor-legs-plate-*": { "base":  "kobold:entity/humanoid/seraph/armor/plate/legs" }
  } 
}
```



You can also specify wearable shape to replace, instead of wearable item code in 'model-replacements-byshape.json' file
```
{
  "kobold:kobold": {
    "game:entity/humanoid/seraph/armor/plate/legs": "kobold:entity/humanoid/seraph/armor/plate/legs"
  } 
}
```