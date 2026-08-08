import machine
import onewire
import ds18x20
import time

# Broche de données reliée au capteur
data_pin = machine.Pin(17)

# Initialisation du bus 1-Wire et du driver DS18B20
ow = onewire.OneWire(data_pin)
ds = ds18x20.DS18X20(ow)

# Recherche des capteurs présents sur le bus
roms = ds.scan()
print("Capteurs trouvés :", roms)

if not roms:
    print("Aucun capteur détecté, vérifie le câblage et la résistance de pull-up !")

while True:
    ds.convert_temp()          # Lance la conversion sur tous les capteurs
    time.sleep_ms(750)         # Le DS18B20 a besoin d'au moins 750ms pour convertir

    for rom in roms:
        temp = ds.read_temp(rom)
        print("Sonde", rom.hex(), ":", temp, "°C")

    time.sleep(2)