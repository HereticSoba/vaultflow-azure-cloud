import React, { useEffect, useState } from 'react';
import { getTransactions, createTransaction, type Transaction } from './api';
import { CreditCard, DollarSign, PlusCircle, RefreshCw, Bot, Send } from 'lucide-react';

export default function App() {
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState<boolean>(true);

  // Estados Formulario
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [category, setCategory] = useState('');
  const [merchant, setMerchant] = useState('');

  // Estados Agente IA
  const [prompt, setPrompt] = useState('');
  const [aiResponse, setAiResponse] = useState('');
  const [loadingAi, setLoadingAi] = useState(false);

  const fetchTransactions = async () => {
    setLoading(true);
    try {
      const data = await getTransactions();
      setTransactions(data);
    } catch (error) {
      console.error("Error al obtener transacciones:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTransactions();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description || !amount) return;

    try {
      await createTransaction({
        description,
        amount: parseFloat(amount),
        category: category || 'General',
        merchant: merchant || 'Desconocido'
      });

      setDescription('');
      setAmount('');
      setCategory('');
      setMerchant('');
      fetchTransactions();
    } catch (error) {
      console.error("Error al guardar la transacción:", error);
    }
  };

  const handleAskAI = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prompt.trim()) return;

    setLoadingAi(true);
    setAiResponse('');

    try {
      const res = await fetch('https://app-vaultflow-api.wittygrass-3fa04590.westus3.azurecontainerapps.io/api/ai/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          prompt,
          transactions: transactions.map(t => ({
            description: t.description,
            amount: t.amount,
            category: t.category,
            merchant: t.merchant
          }))
        })
      });

      const data = await res.json();
      if (res.ok) {
        setAiResponse(data.response);
      } else {
        setAiResponse(`Error (${res.status}): ${data.error || 'Ocurrió un problema con el agente'}`);
      }
    } catch (error) {
      console.error("Error en la consulta de IA:", error);
      setAiResponse("Error de conexión al consultar el servicio de IA en Azure.");
    } finally {
      setLoadingAi(false);
    }
  };

  const totalBalance = transactions.reduce((acc, curr) => acc + curr.amount, 0);

  return (
    <div style={{ minHeight: '100vh', backgroundColor: '#090d16', color: '#f1f5f9', fontFamily: 'system-ui, -apple-system, sans-serif', padding: '2rem 1.5rem' }}>
      <div style={{ maxWidth: '1150px', margin: '0 auto' }}>

        {/* HEADER */}
        <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem', borderBottom: '1px solid #1e293b', paddingBottom: '1.5rem' }}>
          <div>
            <h1 style={{ fontSize: '2.25rem', fontWeight: '800', margin: 0, background: 'linear-gradient(90deg, #38bdf8 0%, #6366f1 100%)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
              VaultFlow Dashboard
            </h1>
            <p style={{ color: '#94a3b8', margin: '0.35rem 0 0 0', fontSize: '0.95rem' }}>
              Gestión Financiera Cloud-Native en Azure
            </p>
          </div>
          <button
            onClick={fetchTransactions}
            style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.6rem 1.2rem', background: '#1e293b', color: '#38bdf8', border: '1px solid #334155', borderRadius: '0.5rem', cursor: 'pointer', fontWeight: '600', transition: 'all 0.2s' }}>
            <RefreshCw size={16} /> Actualizar
          </button>
        </header>

        {/* BALANCE CARD */}
        <div style={{ background: 'linear-gradient(135deg, #1d4ed8 0%, #4338ca 100%)', padding: '2rem', borderRadius: '1rem', marginBottom: '2rem', boxShadow: '0 10px 25px -5px rgba(37, 99, 235, 0.3)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#bfdbfe', fontSize: '0.95rem', fontWeight: '500' }}>
            <DollarSign size={20} />
            <span>Balance Total acumulado</span>
          </div>
          <h2 style={{ fontSize: '3rem', fontWeight: '900', margin: '0.5rem 0 0 0', letterSpacing: '-0.025em' }}>
            ${totalBalance.toFixed(2)}
          </h2>
        </div>

        {/* GRID DE 3 COLUMNAS: FORMULARIO - HISTORIAL - ASISTENTE IA */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.75rem' }}>

          {/* COLUMNA 1: FORMULARIO NUEVA TRANSACCIÓN */}
          <div style={{ background: '#0f172a', padding: '1.5rem', borderRadius: '1rem', border: '1px solid #1e293b', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.5)' }}>
            <h3 style={{ margin: '0 0 1.25rem 0', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#f8fafc', fontSize: '1.15rem' }}>
              <PlusCircle size={18} color="#38bdf8" /> Nueva Transacción
            </h3>
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: '600', color: '#94a3b8', marginBottom: '0.35rem' }}>Descripción</label>
                <input
                  type="text"
                  placeholder="Ej. Suscripción Azure"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  style={{ width: '100%', padding: '0.65rem', borderRadius: '0.5rem', background: '#020617', border: '1px solid #334155', color: '#f8fafc', boxSizing: 'border-box', outline: 'none' }}
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: '600', color: '#94a3b8', marginBottom: '0.35rem' }}>Monto ($)</label>
                <input
                  type="number"
                  step="0.01"
                  placeholder="0.00"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  style={{ width: '100%', padding: '0.65rem', borderRadius: '0.5rem', background: '#020617', border: '1px solid #334155', color: '#f8fafc', boxSizing: 'border-box', outline: 'none' }}
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: '600', color: '#94a3b8', marginBottom: '0.35rem' }}>Categoría</label>
                <input
                  type="text"
                  placeholder="Ej. Cloud / Licencias"
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  style={{ width: '100%', padding: '0.65rem', borderRadius: '0.5rem', background: '#020617', border: '1px solid #334155', color: '#f8fafc', boxSizing: 'border-box', outline: 'none' }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: '600', color: '#94a3b8', marginBottom: '0.35rem' }}>Comercio</label>
                <input
                  type="text"
                  placeholder="Ej. Microsoft"
                  value={merchant}
                  onChange={(e) => setMerchant(e.target.value)}
                  style={{ width: '100%', padding: '0.65rem', borderRadius: '0.5rem', background: '#020617', border: '1px solid #334155', color: '#f8fafc', boxSizing: 'border-box', outline: 'none' }}
                />
              </div>

              <button
                type="submit"
                style={{ marginTop: '0.5rem', padding: '0.8rem', background: '#2563eb', color: 'white', border: 'none', borderRadius: '0.5rem', fontWeight: '700', cursor: 'pointer', boxShadow: '0 4px 12px rgba(37, 99, 235, 0.3)' }}>
                Guardar Transacción
              </button>
            </form>
          </div>

          {/* COLUMNA 2: HISTORIAL DE TRANSACCIONES */}
          <div style={{ background: '#0f172a', padding: '1.5rem', borderRadius: '1rem', border: '1px solid #1e293b', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.5)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#f8fafc', fontSize: '1.15rem' }}>
                <CreditCard size={18} color="#818cf8" /> Historial
              </h3>
              <span style={{ fontSize: '0.75rem', fontWeight: '700', background: '#1e293b', color: '#cbd5e1', padding: '0.2rem 0.6rem', borderRadius: '1rem' }}>
                {transactions.length} registros
              </span>
            </div>

            {loading ? (
              <p style={{ color: '#94a3b8', fontSize: '0.9rem' }}>Cargando datos desde Azure SQL...</p>
            ) : transactions.length === 0 ? (
              <p style={{ color: '#64748b', fontSize: '0.9rem' }}>No hay transacciones guardadas aún.</p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxHeight: '380px', overflowY: 'auto' }}>
                {transactions.map((t) => (
                  <div key={t.id} style={{ background: '#020617', padding: '1rem', borderRadius: '0.625rem', border: '1px solid #1e293b', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                      <strong style={{ display: 'block', color: '#f1f5f9', fontSize: '0.95rem' }}>{t.description}</strong>
                      <small style={{ color: '#94a3b8', fontSize: '0.8rem' }}>{t.category} • <span style={{ color: '#cbd5e1' }}>{t.merchant}</span></small>
                    </div>
                    <span style={{ fontWeight: '700', color: '#34d399', fontSize: '0.95rem' }}>
                      +${t.amount.toFixed(2)}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* COLUMNA 3: ASISTENTE DE IA */}
          <div style={{ background: '#0f172a', padding: '1.5rem', borderRadius: '1rem', border: '1px solid #1e293b', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.5)', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
            <div>
              <h3 style={{ margin: '0 0 1.25rem 0', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#f8fafc', fontSize: '1.15rem' }}>
                <Bot size={18} color="#c084fc" /> Asistente IA VaultFlow
              </h3>

              <form onSubmit={handleAskAI} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                <textarea
                  rows={3}
                  placeholder="Ej: ¿Cuánto gasté en suscripciones?"
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  style={{ width: '100%', padding: '0.75rem', borderRadius: '0.5rem', background: '#020617', border: '1px solid #334155', color: '#f8fafc', boxSizing: 'border-box', outline: 'none', resize: 'none', fontSize: '0.9rem' }}
                />
                <button
                  type="submit"
                  disabled={loadingAi}
                  style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem', padding: '0.75rem', background: '#9333ea', color: 'white', border: 'none', borderRadius: '0.5rem', fontWeight: '700', cursor: 'pointer', opacity: loadingAi ? 0.6 : 1, transition: 'all 0.2s' }}>
                  <Send size={16} /> {loadingAi ? 'Consultando...' : 'Preguntar a la IA'}
                </button>
              </form>
            </div>

            {/* RESPUESTA DEL AGENTE */}
            <div style={{ marginTop: '1.25rem', padding: '1rem', background: '#020617', border: '1px solid #2e1065', borderRadius: '0.625rem', minHeight: '110px' }}>
              {loadingAi ? (
                <p style={{ color: '#c084fc', margin: 0, fontSize: '0.85rem' }}>Procesando en Azure Container App...</p>
              ) : aiResponse ? (
                <p style={{ color: '#e2e8f0', margin: 0, fontSize: '0.85rem', lineHeight: '1.5' }}>{aiResponse}</p>
              ) : (
                <p style={{ color: '#64748b', margin: 0, fontSize: '0.85rem' }}>La respuesta del modelo en tiempo real aparecerá aquí.</p>
              )}
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}