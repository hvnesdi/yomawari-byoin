import asyncio
import json
import os

import discord
import websockets

WEBSOCKET_HOST = "localhost"
WEBSOCKET_PORT = 8765

intents = discord.Intents.default()
intents.voice_states = True
intents.members = True

client = discord.Client(intents=intents)
connected_clients: set = set()


async def websocket_handler(websocket):
    connected_clients.add(websocket)
    try:
        await websocket.wait_closed()
    finally:
        connected_clients.discard(websocket)


async def broadcast(data: dict):
    if not connected_clients:
        return
    msg = json.dumps(data)
    await asyncio.gather(
        *[ws.send(msg) for ws in connected_clients],
        return_exceptions=True,
    )


@client.event
async def on_ready():
    print(f"Bot ready: {client.user}")


@client.event
async def on_voice_state_update(member, before, after):
    await broadcast({
        "type": "voice_state",
        "user": str(member),
        "speaking": after.channel is not None,
    })


async def main():
    token = os.environ.get("DISCORD_BOT_TOKEN", "YOUR_TOKEN_HERE")
    async with websockets.serve(websocket_handler, WEBSOCKET_HOST, WEBSOCKET_PORT):
        await client.start(token)


if __name__ == "__main__":
    asyncio.run(main())
