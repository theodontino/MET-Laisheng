import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './style.css';
import './compact/compact.css';
class Boundary extends React.Component<{children:React.ReactNode},{error:string}>{
 state={error:''};static getDerivedStateFromError(error:Error){return{error:error.message}}
 render(){return this.state.error?<div className="error-boundary"><h2>演示页面遇到问题</h2><p>{this.state.error}</p><button className="btn primary" onClick={()=>location.reload()}>重新打开演示</button></div>:this.props.children}
}
createRoot(document.getElementById('root')!).render(<Boundary><App/></Boundary>);
