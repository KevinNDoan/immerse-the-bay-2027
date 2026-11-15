"""Serve explicitly prepared demo worlds by node ID, without any environment credentials."""
import argparse
from pathlib import Path

import uvicorn
from dotenv import dotenv_values

from zap_relay.app import create_app
from zap_relay.collection import WorldCollection
from zap_relay.config import DEFAULT_MODEL, Settings


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog',type=Path,required=True)
    parser.add_argument('--port',type=int,default=8794)
    parser.add_argument('--enable-openai',action='store_true')
    args=parser.parse_args()
    collection=WorldCollection(args.catalog)
    key,model='','gpt-6-luna'
    if args.enable_openai:
        root=Path(__file__).resolve().parents[2]
        env={**dotenv_values(root/'relay'/'.env'),**dotenv_values(root/'.env')}
        key=(env.get('OPENAI_API_KEY') or '').strip(); model=env.get('NODE_MODEL') or model
        if not key: parser.error('OPENAI_API_KEY is missing.')
    settings=Settings('standin','',DEFAULT_MODEL,args.port,openai_key=key,node_model=model,
                      panorama_model='',world_provider='worldlabs',world_generation_enabled=False)
    app=create_app(settings)
    app.state.spatial.collection=collection; app.state.spatial.enabled=True
    print('Prepared world collection loaded. All environment generation/export is disabled.',flush=True)
    uvicorn.run(app,host='127.0.0.1',port=args.port)


if __name__=='__main__': main()
