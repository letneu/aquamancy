import network
import urequests
import machine
import ds18x20
import time
import onewire
import json
import os
# v1.0 du 25/01 a 19:20x
# Voir github.com/letneu/aquamancy/wiki

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
    "wifi_ssid": "",
    "wifi_password": "",
    "server_url": "http://192.168.1.126:5000/api/submit"
}
# Sur les anciens modèles c'est 28 et 27 pour les nouveaux
temperature_data_ping = 27

# -------------------------
# FONCTIONS
# -------------------------

# Fonction de récupération de l'ID unique de la machine
def get_unique_id():
    while True:
        try:
            # Id unique mais un peu long et moche
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
    try:
        # Connexion à la sonde sur le GPIO
        datapin = machine.Pin(temperature_data_ping)
        ow = onewire.OneWire(datapin)
        ds = ds18x20.DS18X20(ow)

        # On est en onewire mais on a besoin de gérer qu'un seul truc (la sonde))
        roms = ds.scan()
        if not roms:
            raise Exception("Aucune sonde détectée")
        rom = roms[0]
    except Exception as e:
        # Erreur de connexion à la sonde, code 2
        error_blink(2, 60)
        print("Erreur de connexion à la sonde :", e)
        return False
    return True

# Fonction de connexion au réseau Wi-Fi
def wifi_connect():
    global wlan, server_url
    try:
        # Creation du fichier de config si n'existe pas
        if "config.json" not in os.listdir():
            with open("config.json", "w") as f:
                json.dump(DEFAULT_CONFIG, f)

        # Miam miam le fichier
        with open("config.json") as f:
            config = json.load(f)

        # Setup des variables
        wifi_ssid = config["wifi_ssid"]
        wifi_password = config["wifi_password"]
        server_url = config.get("server_url", DEFAULT_CONFIG["server_url"])
    except Exception as e:
        # Erreur de récupération du fichier de config, code 7
        error_blink(7, 60)
        print("Erreur de lecture du fichier de configuration :", e)
        return False

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
    except Exception as e:
        # Erreur de connexion au wifi, code 3
        error_blink(3, 60)
        print("Erreur de connexion au wifi :", e)
        return False
    return True

# Fonction de clignotement en cas d'erreur
def error_blink(blink_count, duration):
    blink_duration = 0.5
    pause_duration = 4

    cycle_duration = (blink_duration * 2) * blink_count + pause_duration
    cycle_count = int(duration / cycle_duration)

    # On clignote un certain nombre de fois puis dodo pendant [pause_duration]
    for y in range(cycle_count):
        for i in range(blink_count):
            statusLed.value(1)    
            time.sleep(blink_duration)
            statusLed.value(0) 
            time.sleep(blink_duration)
        time.sleep(pause_duration)

# Fonction de gestion des exceptions
def handle_exception(e, error_code):
    print("Erreur :", e)
        
    # Activer le clignotement d'erreur
    error_blink(error_code, 60)

    # Risque de boucle infinie si wifi ou sonde KO, mais d'un autre côté, on ne peut pas continuer ça :(
            
    # Tentative de reco wifi
    while not wifi_connect():
        time.sleep(1)
        
    # Tentative de reco à la sonde
    while not probe_connect():
        time.sleep(1)

# -------------------------
# PHASE D'INITIALISATION
# -------------------------

# Variables globales pour la sonde de temperature
ds = None
rom = None

# Variable globale pour la connexion WiFi
wlan = None

# Variable globale pour l'URL du serveur
server_url = None

# Indicateur pour identifier un reboot côté serveur
first_loop = True

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
        # temps nécessaire pour la conversion, c'est comme ça dans la doc :(
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
        
        rssi = wlan.status('rssi')
        print("RSSI :", rssi, " db")
        
        payload = {
            "MachineName": uid,
            "Temperature": str(temp),
            "Rssi": rssi,
            "FirstLoop": first_loop
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
        
    first_loop = False

    # Led allumée pendant la période d'attente pour indiquer que tout va bien
    statusLed.value(1)

    # Attente avant le prochain envoi en fonction de la configuration dans la table probe
    time.sleep(sendFrequencyInSeconds)


