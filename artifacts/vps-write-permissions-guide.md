# VPS Ubuntu: permisos de escritura para PUCK

Tu salida indica que existe una plantilla de systemd:

```bash
/etc/systemd/system/puck@.service
WorkingDirectory=/srv/puckserver
ExecStart=/srv/puckserver/Puck.x86_64 --serverConfigurationPath %i.json
```

Pero este comando:

```bash
sudo systemctl list-units --type=service --all | grep 'puck@'
```

te devolvio codigo `1`, o sea: no hay ninguna instancia `puck@...service` cargada ahora mismo.

Eso normalmente significa una de estas dos cosas:

1. El servidor no esta corriendo por systemd en este momento.
2. Se esta arrancando manualmente.
3. La instancia existe pero no esta cargada o no coincide con `puck@`.

## Solucion rapida para arreglar los permisos ya

Si estas dentro de la VPS como `root`, puedes aplicar esto directamente.

```bash
mkdir -p /srv/puckserver/UserData/BotMemory
touch /srv/puckserver/UserData/schrader_ranked_mmr.json

chown -R root:root /srv/puckserver/UserData
chmod 775 /srv/puckserver/UserData
chmod 775 /srv/puckserver/UserData/BotMemory
chmod 664 /srv/puckserver/UserData/schrader_ranked_mmr.json
```

Luego comprueba escritura:

```bash
test -w /srv/puckserver/UserData/schrader_ranked_mmr.json && echo MMR_OK
test -w /srv/puckserver/UserData/BotMemory && echo BOTMEMORY_OK
```

Si ambas lineas responden `MMR_OK` y `BOTMEMORY_OK`, esos permisos ya quedaron bien para un proceso ejecutado como `root`.

## Si el servidor se arranca manualmente

Comprueba con que usuario lo arrancas realmente:

```bash
ps -ef | grep -Ei 'Puck.x86_64|puckserver|dedicated' | grep -v grep
```

Mira la primera columna. Ese es el usuario real.

Si sale por ejemplo `steam`, entonces usa esto:

```bash
mkdir -p /srv/puckserver/UserData/BotMemory
touch /srv/puckserver/UserData/schrader_ranked_mmr.json

chown -R steam:steam /srv/puckserver/UserData
chmod 775 /srv/puckserver/UserData
chmod 775 /srv/puckserver/UserData/BotMemory
chmod 664 /srv/puckserver/UserData/schrader_ranked_mmr.json
```

Y comprueba:

```bash
sudo -u steam test -w /srv/puckserver/UserData/schrader_ranked_mmr.json && echo MMR_OK
sudo -u steam test -w /srv/puckserver/UserData/BotMemory && echo BOTMEMORY_OK
```

## Si quieres arrancarlo por systemd

Como tu plantilla es `puck@.service`, puedes crear una instancia, por ejemplo `ranked`.

Eso usara este archivo de configuracion:

```bash
/srv/puckserver/ranked.json
```

Para arrancarlo asi:

```bash
sudo systemctl start puck@ranked.service
sudo systemctl status puck@ranked.service
```

Y para activarlo al reiniciar:

```bash
sudo systemctl enable puck@ranked.service
```

Antes de eso, revisa con que usuario correria:

```bash
sudo systemctl cat puck@.service
```

Si dentro no aparece una linea `User=...`, entonces correra como `root`.

## Si quieres dejarlo mas limpio con un usuario dedicado

Puedes crear un usuario por ejemplo `puck` y hacer que el servicio corra con ese usuario.

### 1. Crear usuario

```bash
sudo useradd -r -s /usr/sbin/nologin -d /srv/puckserver puck
```

### 2. Darle propiedad de la carpeta

```bash
sudo mkdir -p /srv/puckserver/UserData/BotMemory
sudo touch /srv/puckserver/UserData/schrader_ranked_mmr.json

sudo chown -R puck:puck /srv/puckserver
sudo chmod 775 /srv/puckserver/UserData
sudo chmod 775 /srv/puckserver/UserData/BotMemory
sudo chmod 664 /srv/puckserver/UserData/schrader_ranked_mmr.json
```

### 3. Editar la plantilla systemd

Abre:

```bash
sudo nano /etc/systemd/system/puck@.service
```

Y asegurate de que tenga una linea asi dentro de `[Service]`:

```ini
User=puck
Group=puck
WorkingDirectory=/srv/puckserver
ExecStart=/srv/puckserver/Puck.x86_64 --serverConfigurationPath %i.json
```

### 4. Recargar systemd y arrancar

```bash
sudo systemctl daemon-reload
sudo systemctl start puck@ranked.service
sudo systemctl status puck@ranked.service
```

### 5. Comprobar permisos con ese usuario

```bash
sudo -u puck test -w /srv/puckserver/UserData/schrader_ranked_mmr.json && echo MMR_OK
sudo -u puck test -w /srv/puckserver/UserData/BotMemory && echo BOTMEMORY_OK
```

## Comando minimo recomendado para tu caso actual

Como ahora mismo solo has confirmado la plantilla y no una instancia activa, el arreglo minimo y directo es este:

```bash
mkdir -p /srv/puckserver/UserData/BotMemory
touch /srv/puckserver/UserData/schrader_ranked_mmr.json
chown -R root:root /srv/puckserver/UserData
chmod 775 /srv/puckserver/UserData
chmod 775 /srv/puckserver/UserData/BotMemory
chmod 664 /srv/puckserver/UserData/schrader_ranked_mmr.json
```

Y luego probar:

```bash
test -w /srv/puckserver/UserData/schrader_ranked_mmr.json && echo MMR_OK
test -w /srv/puckserver/UserData/BotMemory && echo BOTMEMORY_OK
```

## Despues de arreglar permisos

Vuelve a lanzar el servidor y revisa que desaparezcan estos errores del log:

```text
Access to the path "/srv/puckserver/UserData/schrader_ranked_mmr.json" is denied.
Access to the path "/srv/puckserver/UserData/BotMemory/..." is denied.
```

Si aun salen, entonces el servidor no esta corriendo como `root` sino como otro usuario. En ese caso usa el bloque de `ps -ef` de arriba para descubrirlo y cambia el `chown` a ese usuario.