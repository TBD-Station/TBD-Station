import os
import re

# Define the text
text = """
Resources/Textures/Objects/Weapons/Guns/Projectiles/projectiles2.rsi: PNG not defined in metadata: ap.png
Resources/Textures/Objects/Weapons/Guns/Battery/laser_gun.rsi: PNG not defined in metadata: equipped-BACKPACK.png
Resources/Textures/Objects/Weapons/Guns/Battery/laser_gun.rsi: PNG not defined in metadata: inhand-right.png
Resources/Textures/Objects/Weapons/Guns/Battery/laser_gun.rsi: PNG not defined in metadata: inhand-left.png
Resources/Textures/Objects/Weapons/Guns/Battery/laser_gun.rsi: PNG not defined in metadata: equipped-SUITSTORAGE.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: uranium-2.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: piercing-1.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: practice-3.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: piercing-5.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: practice-1.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: practice-6.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: piercing-2.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: uranium-3.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: uranium-6.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: piercing-4.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: piercing-3.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: piercing-6.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: practice-5.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: practice-2.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: uranium-5.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: uranium-4.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: practice-4.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/SpeedLoaders/Magnum/magnum_speed_loader.rsi: PNG not defined in metadata: uranium-1.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: blank-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: flare-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: improvised-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: blank.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: base-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: incendiary-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: beanbag-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: tranquilizer.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: slug-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: depleted-uranium-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: beanbag.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: practice-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/shotgun_shell.rsi: PNG not defined in metadata: tranquilizer-spent.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Boxes/rifle.rsi: PNG not defined in metadata: mag-big-1.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Boxes/rifle.rsi: PNG not defined in metadata: base-big.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Boxes/rifle.rsi: PNG not defined in metadata: mag-big-3.png
Resources/Textures/Objects/Weapons/Guns/Ammunition/Boxes/rifle.rsi: PNG not defined in metadata: mag-big-2.png
"""

# Use regular expression to extract the file paths
file_paths = []
for line in text.splitlines():
    match = re.search(r": PNG not defined in metadata: (.+)", line)
    if match:
        file_name = match.group(1)
        folder_path = line.split(":")[0]
        file_path = os.path.join(folder_path, file_name)
        file_paths.append(file_path)

# Define a function to delete the files
def delete_files(file_paths):
    for file_path in file_paths:
        if os.path.exists(file_path):
            os.remove(file_path)
            print(f"Deleted file: {file_path}")
        else:
            print(f"File not found: {file_path}")

# Call the function
delete_files(file_paths)
