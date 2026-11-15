"""Serve prepared worlds, optionally allowing paid generation for new ideas."""
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
    parser.add_argument('--enable-generation',action='store_true',help='Allow paid World Labs generation for new node IDs')
    parser.add_argument('--worldlabs-key-env',default='WORLD_LABS_API_KEY')
    args=parser.parse_args()
    collection=WorldCollection(args.catalog)
    key,model='','gpt-6-luna'
    root=Path(__file__).resolve().parents[2]
    env={**dotenv_values(root/'relay'/'.env'),**dotenv_values(root/'.env')}
    world_key=(env.get(args.worldlabs_key_env) or '').strip() if args.enable_generation else ''
    if args.enable_generation and not world_key: parser.error('The selected World Labs credential is missing.')
    if args.enable_openai:
        key=(env.get('OPENAI_API_KEY') or '').strip(); model=env.get('NODE_MODEL') or model
        if not key: parser.error('OPENAI_API_KEY is missing.')
    settings=Settings('standin','',DEFAULT_MODEL,args.port,openai_key=key,node_model=model,
                      panorama_model='',world_provider='worldlabs',world_generation_enabled=args.enable_generation,
                      worldlabs_key=world_key)
    app=create_app(settings)
    app.state.spatial.collection=collection; app.state.spatial.enabled=True
    print('Prepared worlds loaded. New idea generation: '+('ENABLED (paid; balance checked before each job).' if args.enable_generation else 'disabled.'),flush=True)
    uvicorn.run(app,host='127.0.0.1',port=args.port)


if __name__=='__main__': main()
