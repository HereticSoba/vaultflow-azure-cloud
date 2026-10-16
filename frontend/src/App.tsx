import React, { useEffect, useState } from 'react';
import { getTransactions, createTransaction, type Transaction } from './api';
import { CreditCard, DollarSign, PlusCircle, RefreshCw } from 'lucide-react';

export default function App() {
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState<boolean>(true);

  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [category, setCategory] = useState('');
  const [merchant, setMerchant] = useState('');

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

  const totalBalance = transactions.reduce((acc, curr) => acc + curr.amount, 0);

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: '900px', margin: '0 auto', color: '#1a202c' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
        <div>
          <h1 style={{ fontSize: '2rem', fontWeight: 'bold', margin: 0, color: '#2563eb' }}>VaultFlow Dashboard</h1>
          <p style={{ color: '#6b7280', margin: '0.5rem 0 0 0' }}>Gestión Financiera Cloud-Native en Azure</p>
        </div>
        <button
          onClick={fetchTransactions}
          style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem', background: '#e0e7ff', color: '#3730a3', border: 'none', borderRadius: '0.375rem', cursor: 'pointer' }}>
          <RefreshCw size={16} /> Actualizar
        </button>
      </header>

      <div style={{ background: 'linear-gradient(135deg, #2563eb 0%, #1e40af 100%)', color: 'white', padding: '1.5rem', borderRadius: '0.75rem', marginBottom: '2rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', opacity: 0.9 }}>
          <DollarSign size={20} />
          <span>Balance Total acumulado</span>
        </div>
        <h2 style={{ fontSize: '2.5rem', fontWeight: 'bold', margin: '0.5rem 0 0 0' }}>
          ${totalBalance.toFixed(2)}
        </h2>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2rem' }}>
        <div style={{ background: '#f9fafb', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <h3 style={{ margin: '0 0 1rem 0', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <PlusCircle size={18} /> Nueva Transacción
          </h3>
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <input
              type="text"
              placeholder="Descripción"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              style={{ padding: '0.5rem', borderRadius: '0.375rem', border: '1px solid #d1d5db' }}
              required
            />
            <input
              type="number"
              step="0.01"
              placeholder="Monto ($)"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              style={{ padding: '0.5rem', borderRadius: '0.375rem', border: '1px solid #d1d5db' }}
              required
            />
            <input
              type="text"
              placeholder="Categoría"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              style={{ padding: '0.5rem', borderRadius: '0.375rem', border: '1px solid #d1d5db' }}
            />
            <input
              type="text"
              placeholder="Comercio"
              value={merchant}
              onChange={(e) => setMerchant(e.target.value)}
              style={{ padding: '0.5rem', borderRadius: '0.375rem', border: '1px solid #d1d5db' }}
            />
            <button
              type="submit"
              style={{ padding: '0.75rem', background: '#2563eb', color: 'white', border: 'none', borderRadius: '0.375rem', fontWeight: 'bold', cursor: 'pointer' }}>
              Guardar Transacción
            </button>
          </form>
        </div>

        <div>
          <h3 style={{ margin: '0 0 1rem 0', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <CreditCard size={18} /> Historial ({transactions.length})
          </h3>
          {loading ? (
            <p style={{ color: '#6b7280' }}>Cargando datos desde Azure SQL...</p>
          ) : transactions.length === 0 ? (
            <p style={{ color: '#6b7280' }}>No hay transacciones guardadas aún.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {transactions.map((t) => (
                <div key={t.id} style={{ background: 'white', padding: '1rem', borderRadius: '0.5rem', border: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <strong style={{ display: 'block', color: '#111827' }}>{t.description}</strong>
                    <small style={{ color: '#6b7280' }}>{t.category} • {t.merchant}</small>
                  </div>
                  <span style={{ fontWeight: 'bold', color: '#16a34a' }}>
                    +${t.amount.toFixed(2)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}