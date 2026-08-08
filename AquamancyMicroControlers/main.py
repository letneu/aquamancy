import network
import urequests
import machine
from machine import ADC, Pin
import ds18x20
import time
import onewire
import json
import os
# v1.0 du 25/01 a 19:20x
# Voir github.com/letneu/aquamancy/wiki

# Codes d'erreur (nombre de clignotement de la led) :
# 2 : Erreur de connexion à la sonde de température
# 3 : Erreur de connexion au wifi
# 4 : Erreur de lecture de la sonde de température
# 5 : Erreur de communication avec le serveur
# 6 : Erreur de récupération de l'ID unique de la machine
# 7 : Erreur de lecture du fichier de configuration
# 8 : Erreur de lecture de la sonde de turbidité


print("Lancement de aquamancy")

# Sur les anciens modèles c'est 28 et 27 pour les nouveaux
TEMPERATURE_DATA_PIN = 17

# Broche de la sonde TDS (turbidité)
TDS_PIN = 28
VREF = 3.3
ADC_RESOLUTION = 65535
TDS_SAMPLES = 30

# --- Configuration des broches pour la led ---
LED_PIN_R = 10
LED_PIN_G = 11
LED_PIN_B = 12

FREQ = 1000  # frequence PWM en Hz

DEFAULT_LED_COLOR = (0, 255, 0)

# -------------------------
# FONCTIONS
# -------------------------

# Fonction de lecture de la tension de la sonde TDS
def read_tds_voltage():
    raw = tds_adc.read_u16()
    return raw * VREF / ADC_RESOLUTION

# Fonction de conversion tension -> TDS en ppm (formule DFRobot)
def voltage_to_tds(voltage, temperature=25.0):
    compensation_coefficient = 1.0 + 0.02 * (temperature - 25.0)
    compensated_voltage = voltage / compensation_coefficient
    tds_value = (133.42 * compensated_voltage**3
                 - 255.86 * compensated_voltage**2
                 + 857.39 * compensated_voltage) * 0.5
    return tds_value

# Fonction de lecture du TDS (moyenne sur TDS_SAMPLES mesures)
def read_tds_ppm(temperature=25.0):
    samples = [read_tds_voltage() for _ in range(TDS_SAMPLES)]
    avg_voltage = sum(samples) / len(samples)
    tds_ppm = voltage_to_tds(avg_voltage, temperature)
    return tds_ppm, avg_voltage

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
def temperature_probe_connect():
    global ds, rom
    try:
        # Connexion à la sonde sur le GPIO
        datapin = machine.Pin(TEMPERATURE_DATA_PIN)
        ow = onewire.OneWire(datapin)
        ds = ds18x20.DS18X20(ow)

        # On est en onewire mais on a besoin de gérer qu'un seul truc (la sonde))
        roms = ds.scan()
        if not roms:
            raise Exception("Aucune sonde détectée")
        rom = roms[0]

        # Vérification de la valeur
        ds.convert_temp()
        time.sleep_ms(750)
        temp = ds.read_temp(rom)
        if temp <= 0:
            raise Exception("Température invalide : {}".format(temp))
    except Exception as e:
        print("Erreur de connexion à la sonde de température :", e)
        # Erreur de connexion à la sonde de température, code 2
        error_blink(2, 60)
        return False
    return True

# Fonction de vérification de la sonde de turbidité
def turbidity_probe_connect():
    try:
        tds_ppm, _ = read_tds_ppm()
        if tds_ppm <= 0:
            raise Exception("TDS invalide : {:.0f} ppm".format(tds_ppm))
    except Exception as e:
        print("Erreur de connexion à la sonde de turbidité :", e)
        return False
    return True

# Fonction de connexion au réseau Wi-Fi
def wifi_connect():
    global wlan, server_url
    try:
        # Creation du fichier de config si n'existe pas
        if "config.json" not in os.listdir():
            with open("config.json", "w") as f:
                json.dump({
                "wifi_ssid": "",
                "wifi_password": "",
                "server_base_url": "",
                "server_submit_url" : "/api/submit"
            }, f)

        # Miam miam le fichier
        with open("config.json") as f:
            config = json.load(f)

        # Setup des variables
        wifi_ssid = config["wifi_ssid"]
        wifi_password = config["wifi_password"]
        server_base_url = config.get("server_base_url")
        server_submit_url = config.get("server_submit_url")
        server_url = server_base_url + server_submit_url
    except Exception as e:
        # Erreur de récupération du fichier de config, code 7
        print("Erreur de lecture du fichier de configuration :", e)
        # Fichier corrompu : on le supprime pour qu'il soit recréé au prochain démarrage
        if "config.json" in os.listdir():
            os.remove("config.json")
        error_blink(7, 60)
        return False

    try:
        wlan = network.WLAN(network.STA_IF)
        wlan.active(True)
        wlan.connect(wifi_ssid, wifi_password)

        print("Connexion Wi-Fi… ({} / {})".format(wifi_ssid, wifi_password))

        for y in range(10):
            time.sleep(2)
            if wlan.isconnected():
                break
     
        if not wlan.isconnected():
            raise Exception("Échec de la connexion Wi-Fi")

        print("Connecté :", wlan.ifconfig())
    except Exception as e:
        print("Erreur de connexion au wifi :", e)
        # Erreur de connexion au wifi, code 3
        error_blink(3, 60)
        return False
    return True

# Set de la couleur de la led
def led_set_color(r, g, b):
    # duty_u16 attend une valeur entre 0 et 65535
    pwm_r.duty_u16(int(r / 255 * 65535))
    pwm_g.duty_u16(int(g / 255 * 65535))
    pwm_b.duty_u16(int(b / 255 * 65535))

# Extinction de la led
def led_turn_off():
    led_set_color(0, 0, 0)

# Verif des valeurs RGB
def clamp_color_value(value, default):
    try:
        value = int(value)
    except (TypeError, ValueError):
        print("clamp_color_value - valeur invalide ({!r}), utilisation du défaut {}".format(value, default))
        return default

    if value < 0:
        print("clamp_color_value - valeur {} < 0, ramenée à 0".format(value))
        return 0
    if value > 255:
        print("clamp_color_value - valeur {} > 255, ramenée à 255".format(value))
        return 255
    return value

# Parse de la réponse du serveur pour la led
def get_response_led_color(response_data):
    default_r, default_g, default_b = DEFAULT_LED_COLOR

    raw_r = response_data.get("colorR")
    raw_g = response_data.get("colorG")
    raw_b = response_data.get("colorB")
    print("get_response_led_color - valeurs brutes reçues :", raw_r, raw_g, raw_b)

    color = (
        clamp_color_value(raw_r, default_r),
        clamp_color_value(raw_g, default_g),
        clamp_color_value(raw_b, default_b),
    )
    print("get_response_led_color - couleur calculée (R, G, B) :", color)

    return color


# Fonction de clignotement en cas d'erreur
def error_blink(blink_count, duration):
    blink_duration = 0.5
    pause_duration = 4

    cycle_duration = (blink_duration * 2) * blink_count + pause_duration
    cycle_count = int(duration / cycle_duration)

    # On clignote un certain nombre de fois puis dodo pendant [pause_duration]
    for y in range(cycle_count):
        for i in range(blink_count):
            led_set_color(255, 0, 0)
            time.sleep(blink_duration)
            led_turn_off()
            time.sleep(blink_duration)
        time.sleep(pause_duration)

# Fonction de gestion des exceptions
def handle_exception(e, error_code):
    print("Erreur :", e)
        
    # Activer le clignotement d'erreur
    error_blink(error_code, 60)

    # Reconnecter seulement la ressource liée à l'erreur pour éviter de masquer
    # un problème serveur temporaire par des cycles de reconnexion inutiles.
    if error_code == 4:
        while not temperature_probe_connect():
            time.sleep(1)
    elif error_code == 8:
        while not turbidity_probe_connect():
            time.sleep(1)
    elif error_code == 5:
        if wlan is None or not wlan.isconnected():
            while not wifi_connect():
                time.sleep(1)

# -------------------------
# PHASE D'INITIALISATION
# -------------------------

# Variables globales pour la sonde de temperature

ds = None
rom = None

# Variable globale pour la sonde TDS
tds_adc = None

# Variables globales pour la led RGB
pwm_r = None
pwm_g = None
pwm_b = None

# Variable globale pour la connexion WiFi
wlan = None

# Variable globale pour l'URL du serveur
server_url = None

# Indicateur pour identifier un reboot côté serveur
first_loop = True

# Couleur affichée pendant l'attente avant d'avoir reçu une config du serveur
current_led_color = DEFAULT_LED_COLOR

# Fréquence d'envoi par défaut (en secondes)
sendFrequencyInSeconds = 60

# Initialisation de la sonde TDS
tds_adc = ADC(Pin(TDS_PIN))

# Initialisation de la LED de statut
pwm_r = machine.PWM(machine.Pin(LED_PIN_R), freq=FREQ)
pwm_g = machine.PWM(machine.Pin(LED_PIN_G), freq=FREQ)
pwm_b = machine.PWM(machine.Pin(LED_PIN_B), freq=FREQ)

# Clignotement long au démarrage
led_set_color(*DEFAULT_LED_COLOR)
time.sleep(4)
led_turn_off()

# Récupération de l'ID unique de la machine
uid = get_unique_id()

# Connexion à la sonde de température
while not temperature_probe_connect():
    time.sleep(1)

# Vérification de la sonde de turbidité
while not turbidity_probe_connect():
    time.sleep(1)

# Connexion au Wi-Fi
while not wifi_connect():
    time.sleep(1)

# -------------------------
# BOUCLE PRINCIPALE
# -------------------------
while True:
    
    # Led éteinte pendant la lecture de la sonde et l'envoi des données
    led_turn_off()

    # Lecture de la température
    try:
        # temps nécessaire pour la conversion, c'est comme ça dans la doc :(
        ds.convert_temp()
        time.sleep_ms(750)

        temp = ds.read_temp(rom)
        if temp <= 0:
            raise Exception("Température invalide : {}".format(temp))
        print("Température :", temp, "°C")
        
    except Exception as e:
        # Erreur de lecture de la sonde de température, code 4
        handle_exception(e, 4)
        continue


    # Lecture TDS (moyenne sur TDS_SAMPLES mesures)
    try:
        tds_ppm, avg_voltage = read_tds_ppm(temp)
        if tds_ppm <= 0:
            raise Exception("TDS invalide : {:.0f} ppm".format(tds_ppm))
        print("TDS : {:.0f} ppm (tension : {:.3f} V)".format(tds_ppm, avg_voltage))

    except Exception as e:
        # Erreur de lecture de la sonde de turbidité, code 8
        handle_exception(e, 8)
        continue

    try:
        # Vérifier la connexion WiFi avant l'envoi
        if not wlan.isconnected():
            print("WiFi déconnecté, reconnexion...")
            if not wifi_connect():
                raise Exception("Impossible de rétablir la connexion Wi-Fi")
        
        rssi = wlan.status('rssi')
        print("RSSI :", rssi, " db")
        
        payload = {
            "MachineName": uid,
            "Temperature": str(temp),
            "Turbidity": str(round(tds_ppm, 2)),
            "Rssi": rssi,
            "FirstLoop": first_loop
        }

        r = None
        try:
            print("Envoi vers :", server_url)
            r = urequests.post(server_url, json=payload, timeout=15)
            print("Réponse serveur :", r.text)
            responseData = r.json()
        finally:
            if r is not None:
                r.close()

        if not isinstance(responseData, dict):
            raise Exception("Réponse serveur invalide")

        current_led_color = get_response_led_color(responseData)
        print("Couleur serveur :", current_led_color)
        
        try:
            sendFrequencyInSeconds = int(responseData.get("sendFrequencyInSeconds", 60))
        except (TypeError, ValueError):
            sendFrequencyInSeconds = 60

        # S'assurer que la fréquence d'envoi est d'au moins 1 seconde
        if sendFrequencyInSeconds < 1:
            sendFrequencyInSeconds = 1

        print("sendFrequencyInSeconds :", sendFrequencyInSeconds)
        
    except Exception as e:
        # Erreur de communication avec le serveur, code 5
        handle_exception(e, 5)
        continue
        
    first_loop = False

    # Led allumée pendant la période d'attente pour indiquer que tout va bien
    led_set_color(*current_led_color)

    # Attente avant le prochain envoi en fonction de la configuration dans la table probe
    time.sleep(sendFrequencyInSeconds)


