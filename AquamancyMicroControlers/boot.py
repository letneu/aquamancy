# Chargeur de firmware Aquamancy (mise a jour a distance / OTA)
# ATTENTION : ce fichier n'est JAMAIS mis a jour a distance, il doit rester simple et fiable.
# Il se flashe une seule fois en USB, avec main.py.
#
# Fonctionnement :
# - Sans fichier ota.json : rien n'est fait, le /main.py racine se lance normalement (mode historique / USB).
# - Avec ota.json : lance /app/<current>/main.py.
#   Si la version est en attente de validation (pending), le nombre de demarrages est compte.
#   Au-dela de OTA_MAX_ATTEMPTS demarrages sans validation, retour a la version precedente.
# - La validation est faite par main.py apres le premier envoi reussi au serveur.
# Pour revenir au mode USB : supprimer ota.json.

import os
import sys
import json
import machine

OTA_STATE_FILE = "ota.json"
OTA_STATE_TMP_FILE = "ota.tmp"
OTA_APP_DIR = "/app"
OTA_MAX_ATTEMPTS = 3


def ota_app_path(version):
    return "{}/{}/main.py".format(OTA_APP_DIR, version)


def ota_file_exists(path):
    try:
        os.stat(path)
        return True
    except OSError:
        return False


def ota_save_state(state):
    # Ecriture dans un fichier temporaire puis renommage pour eviter un fichier corrompu en cas de coupure
    with open(OTA_STATE_TMP_FILE, "w") as f:
        json.dump(state, f)
    os.rename(OTA_STATE_TMP_FILE, OTA_STATE_FILE)


def ota_rollback(state):
    failed = state.get("current")
    previous = state.get("previous")
    print("OTA - retour a la version precedente :", failed, "->", previous)
    state["current"] = previous
    state["previous"] = None
    state["pending"] = False
    state["attempts"] = 0
    state["rolled_back_from"] = failed
    ota_save_state(state)


def ota_boot():
    if not ota_file_exists(OTA_STATE_FILE):
        return

    try:
        with open(OTA_STATE_FILE) as f:
            state = json.load(f)
    except Exception as e:
        print("OTA - ota.json illisible, lancement du main.py racine :", e)
        return

    if state.get("pending"):
        attempts = state.get("attempts", 0)
        if attempts >= OTA_MAX_ATTEMPTS and state.get("previous"):
            ota_rollback(state)
        else:
            state["attempts"] = attempts + 1
            ota_save_state(state)
            print("OTA - version {} en attente de validation, tentative {}/{}".format(
                state.get("current"), state["attempts"], OTA_MAX_ATTEMPTS))

    current = state.get("current")
    if not current or not ota_file_exists(ota_app_path(current)):
        if state.get("previous") and ota_file_exists(ota_app_path(state["previous"])):
            ota_rollback(state)
            machine.reset()
        print("OTA - firmware introuvable, lancement du main.py racine")
        return

    print("OTA - lancement de", ota_app_path(current))
    sys.path.insert(0, "{}/{}".format(OTA_APP_DIR, current))
    try:
        __import__("main")
    except KeyboardInterrupt:
        # Interruption manuelle (Thonny / REPL) : on rend la main
        raise
    except Exception as e:
        sys.print_exception(e)
    # Le firmware ne doit jamais rendre la main : plantage => redemarrage (et eventuellement rollback)
    machine.reset()


ota_boot()
