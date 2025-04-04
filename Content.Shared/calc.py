import os
import re

# Define the text
text = """
Resources/Textures/Objects/Specific/Medical/medical.rsi: PNG not defined in metadata: bluebrutepack.png
Resources/Textures/Objects/Specific/Medical/medical.rsi: PNG not defined in metadata: bluebrutepack2.png
Resources/Textures/Objects/Specific/Medical/medical.rsi: PNG not defined in metadata: blueointment3.png
Resources/Textures/Objects/Specific/Medical/medical.rsi: PNG not defined in metadata: blueointment.png
Resources/Textures/Objects/Specific/Medical/medical.rsi: PNG not defined in metadata: bluebrutepack3.png
Resources/Textures/Objects/Specific/Medical/medical.rsi: PNG not defined in metadata: blueointment2.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: hamtr-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripleymkii.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripleymkii-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-empty.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: vim-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: darkhonker-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-old.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-broken-old.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: gygax-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: honker-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: darkhonker.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: vim-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-g.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: honker-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: deathripley-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: clarke.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: deathripley-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: deathripley.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: durand-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: gygax-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: vim.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-g-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-g-full.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripley-g-full-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: durand-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: darkhonker-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: honker.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: ripleymkii-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: gygax.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: hamtr.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: clarke-open.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: durand.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: hamtr-broken.png
Resources/Textures/Objects/Specific/Mech/mecha.rsi: PNG not defined in metadata: clarke-broken.png
Resources/Textures/Objects/Weapons/Guns/Projectiles/projectiles2.rsi: PNG not defined in metadata: incendiary.png
Resources/Textures/Objects/Weapons/Guns/Projectiles/projectiles2.rsi: PNG not defined in metadata: fmj.png
Resources/Textures/Objects/Weapons/Guns/Projectiles/projectiles2.rsi: PNG not defined in metadata: sp.png
"""

import re
import os

# Define the regular expression pattern
pattern = r": PNG not defined in metadata: (.+)"
file_paths = []

# Iterate over each line in the text
for line in text.splitlines():
    # Skip empty lines
    if not line:
        continue

    # Search for the pattern in the line
    line = line.strip().replace(": PNG not defined in metadata: ", "/")
    file_paths.append(line)

# Define a function to delete the files
def delete_files(file_paths):
    for file_path in file_paths:
        if os.path.exists(file_path):
            try:
                os.remove(file_path)
                print(f"Deleted file: {file_path}")
            except OSError as e:
                print(f"Error deleting file: {file_path} - {e}")
        else:
            print(f"File not found: {file_path}")

# Call the function
delete_files(file_paths)
