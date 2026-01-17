import network
import urequests
import machine
import ds18x20
import time
import onewire
import json
import os

# Codes d'erreur (nombre de clignotement de la led) :
# 2 : Erreur de connexion à la sonde
# 3 : Erreur de connexion au wifi
# 4 : Erreur de lecture de la sonde
# 5 : Erreur de communication avec le serveur
# 6 : Erreur de récupération de l'ID unique de la machine
# 7 : Erreur de lecture du fichier de configuration

# -------------------------
# CONFIG PAR DÉFAUT
# -------------------------
DEFAULT_CONFIG = {
    "wifi_ssid": "ssid",
    "wifi_password": "password",
    "server_url": "http://192.168.1.126:5000/api/submit"
}

# Fonction de récupération de l'ID unique de la machine
def get_unique_id():
    while True:
        try:
            uid = machine.unique_id().hex()
            print("ID machine :", uid)
            return uid
        except Exception as e:
            # Erreur de récupération de l'ID unique de la machine, code 6
            error_blink(6, 60)
            print("Erreur de récupération de l'ID unique de la machine", e)

# Fonction de connexion à la sonde de température
def probe_connect():
    global ds, rom
    while True:
        try:
            datapin = machine.Pin(28)
            ow = onewire.OneWire(datapin)
            ds = ds18x20.DS18X20(ow)

            roms = ds.scan()
            if not roms:
                raise Exception("Aucune sonde détectée")
            rom = roms[0]
            return
        except Exception as e:
            # Erreur de connexion à la sonde, code 2
            error_blink(2, 60)
            print("Erreur de connexion à la sonde :", e)

# Fonction de connexion au réseau Wi-Fi
def wifi_connect():
    global wlan, server_url
    while True:
        try:
            # Creation du fichier de config si n'existe pas
            if "config.json" not in os.listdir():
                with open("config.json", "w") as f:
                    json.dump(DEFAULT_CONFIG, f)

            with open("config.json") as f:
                config = json.load(f)

            wifi_ssid = config["wifi_ssid"]
            wifi_password = config["wifi_password"]
            server_url = config.get("server_url", DEFAULT_CONFIG["server_url"])
        except Exception as e:
            # Erreur de récupération du fichier de config, code 7
            error_blink(7, 60)
            print("Erreur de lecture du fichier de configuration :", e)
            continue

        try:
            wlan = network.WLAN(network.STA_IF)
            wlan.active(True)
            wlan.connect(wifi_ssid, wifi_password)

            print("Connexion Wi-Fi…")

            for y in range(10):
                time.sleep(2)
                if wlan.isconnected():
                    break
     
            if not wlan.isconnected():
                raise Exception("Échec de la connexion Wi-Fi")

            print("Connecté :", wlan.ifconfig())
            return
        except Exception as e:
            # Erreur de connexion au wifi, code 3
            error_blink(3, 60)
            print("Erreur de connexion au wifi :", e)

# Fonction de clignotement en cas d'erreur
def error_blink(blink_count, duration):
    cycle_duration = 0.5 * blink_count + 4
    cycle_count = int(duration / cycle_duration)

    for y in range(cycle_count):
        for i in range(blink_count):
            statusLed.value(1)    
            time.sleep(0.25)
            statusLed.value(0) 
            time.sleep(0.25)
        time.sleep(4)

# Fonction de gestion des exceptions
def handle_exception(e, error_code):
    print("Erreur :", e)
        
    # Activer le clignotement d'erreur
    error_blink(error_code, 60)
            
    # Tentative de reco wifi
    wifi_connect()
        
    # Tentative de reco à la sonde
    probe_connect()


# -------------------------
# PHASE D'INITIALISATION
# -------------------------

# Variables globales pour la sonde
ds = None
rom = None

# Variable globale pour la connexion WiFi
wlan = None

# Variable globale pour l'URL du serveur
server_url = None

# Fréquence d'envoi par défaut (en secondes)
sendFrequencyInSeconds = 60

# Initialisation de la LED de statut
statusLed = machine.Pin(1, machine.Pin.OUT)

# Clignotement long au démarrage
statusLed.value(1)
time.sleep(4)
statusLed.value(0)

# Récupération de l'ID unique de la machine
uid = get_unique_id()

# Connexion à la sonde de température
probe_connect()

# Connexion au Wi-Fi
wifi_connect()

# -------------------------
# BOUCLE PRINCIPALE
# -------------------------
while True:
    # Led éteinte pendant la lecture de la sonde et l'envoi des données
    statusLed.value(0)

    # Lecture de la température
    try:
        # temps nécessaire pour la conversion
        ds.convert_temp()
        time.sleep_ms(750)

        temp = ds.read_temp(rom)
        print("Température :", temp, "°C")
        
    except Exception as e:
        # Erreur de lecture de la sonde, code 4
        handle_exception(e, 4)
        continue

    try:
        # Vérifier la connexion WiFi avant l'envoi
        if not wlan.isconnected():
            print("WiFi déconnecté, reconnexion...")
            wifi_connect()
        
        payload = {
            "MachineName": uid,
            "Temperature": str(temp)
        }
    
        r = urequests.post(server_url, json=payload, timeout=15)
        print("Réponse serveur :", r.text)
        responseData = r.json()
        r.close()
        
        sendFrequencyInSeconds = responseData.get("sendFrequencyInSeconds", 60)

        # S'assurer que la fréquence d'envoi est d'au moins 1 seconde
        if not sendFrequencyInSeconds or sendFrequencyInSeconds < 1:
            sendFrequencyInSeconds = 1

        print("sendFrequencyInSeconds :", sendFrequencyInSeconds)
        
    except Exception as e:
        # Erreur de communication avec le serveur, code 5
        handle_exception(e, 5)
        continue
        
    # Led allumée pendant la période d'attente pour indiquer que tout va bien
    statusLed.value(1)

    # Attente avant le prochain envoi en fonction de la configuration dans la table probe
    time.sleep(sendFrequencyInSeconds)


