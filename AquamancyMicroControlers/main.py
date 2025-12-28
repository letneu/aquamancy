import network
import urequests
import machine
import ds18x20
import time
import onewire
from machine import Pin

# -------------------------
# CONFIG WIFI
# -------------------------
WIFI_SSID = "Bbox-Laurent-2.4GHz"
WIFI_PASS = "*Dubonmatin2024"

# -------------------------
# CONFIG SERVEUR
# -------------------------
SERVER_URL = "http://192.168.1.126:5000/api/submit"

# -------------------------
# CONFIG DES LEDS
# -------------------------
statusLed = Pin(1, Pin.OUT)
statusLed.value(1)

def probe_connect():
    # -------------------------
    # DS18B20
    # -------------------------
    datapin = machine.Pin(28)  # GP28
    ow = onewire.OneWire(datapin)
    ds = ds18x20.DS18X20(ow)

    roms = ds.scan()
    return ds, roms[0]

ds, rom = probe_connect()

# -------------------------
# IDENTIFIANT UNIQUE MACHINE
# -------------------------
uid = machine.unique_id().hex()
print("ID machine :", uid)


# -------------------------
# WIFI
# -------------------------
def wifi_connect():
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    wlan.connect(WIFI_SSID, WIFI_PASS)

    print("Connexion Wi-Fi…")
    while not wlan.isconnected():
        time.sleep(0.2)

    print("Connecté :", wlan.ifconfig())
    return wlan

wifi_connect()

# -------------------------
# BOUCLE PRINCIPALE
# -------------------------
while True:
    try:
        ds.convert_temp()
        time.sleep_ms(750)  # temps nécessaire pour la conversion

        temp = ds.read_temp(rom)
        print("Température :", temp, "°C")

        payload = {
            "MachineName": uid,
            "Temperature": str(temp)
        }
    
        r = urequests.post(SERVER_URL, json=payload)
        print("Réponse serveur :", r.text)
        responseData = r.json()
        r.close()
        
        isTooCold = responseData.get("isTooCold")
        isTooHot = responseData.get("isTooHot")
        sendFrequencyInSeconds = responseData.get("sendFrequencyInSeconds")
        
        print("isTooCold :", isTooCold)
        print("isTooHot :", isTooHot)
        print("sendFrequencyInSeconds :", sendFrequencyInSeconds)
        
        # Clignotement court pour indiquer qu'il se passe des trucs
        statusLed.value(0)
        time.sleep(1)
        statusLed.value(1)
        
        time.sleep(sendFrequencyInSeconds)
    except Exception as e:
        print("Erreur :", e)
        
        # Clignote en cas d'erreur
        for i in range(60):
            statusLed.value(i % 2 == 0)    
            time.sleep(1)
            
        # Tentative de reco wifi
        wifi_connect()
        
        # Tentative de reco à la sonde
        ds, rom = probe_connect()


