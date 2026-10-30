"""Send a business command to the dedicated visible RP8 process; no screen input."""
import json
import pathlib
import sys
import time
import uuid

home = pathlib.Path(__file__).resolve().parent
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
deadline = time.monotonic() + 3
while True:
    try:
        session = json.loads((home / 'session.json').read_text(encoding='utf-8'))
        break
    except json.JSONDecodeError:
        if time.monotonic() > deadline:
            raise
        time.sleep(.03)
folder = pathlib.Path(session['sessionDirectory']) / 'requests'
if len(sys.argv) == 3 and sys.argv[1] == '--file':
    request = json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
else:
    request = json.loads(sys.argv[1]) if len(sys.argv) > 1 else {'operation': 'status'}
stem = str(time.time_ns()) + '-' + uuid.uuid4().hex
temporary = folder / (stem + '.tmp')
pending = folder / (stem + '.request.json')
result_path = folder / (stem + '.result.json')
temporary.write_text(json.dumps(request, ensure_ascii=False), encoding='utf-8')
temporary.rename(pending)
deadline = time.monotonic() + 20
while not result_path.exists():
    if time.monotonic() > deadline:
        raise TimeoutError('No RP8 response; inspect session logs before retrying: ' + str(pending))
    time.sleep(.1)
while True:
    try:
        result = json.loads(result_path.read_text(encoding='utf-8'))
        break
    except json.JSONDecodeError:
        if time.monotonic() > deadline:
            raise
        time.sleep(.03)
print(json.dumps(result, ensure_ascii=False))
sys.exit(0 if result['ok'] else 1)
