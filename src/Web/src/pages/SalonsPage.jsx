import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";

export function SalonsPage() {
  const [salons, setSalons] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .listSalons()
      .then(setSalons)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="page">
      <div className="page-head">
        <span className="label">[ 01 ] Directory</span>
        <h1>Salons</h1>
      </div>

      {loading && <p className="state-msg">Loading salons&hellip;</p>}
      {error && <p className="message message-error">{error}</p>}

      {!loading && !error && salons.length === 0 && (
        <p className="state-msg">No salons yet.</p>
      )}

      {!loading && !error && salons.length > 0 && (
        <ul className="salon-grid">
          {salons.map((salon, i) => (
            <li key={salon.id}>
              <Link to={`/salons/${salon.id}`} className="salon-card">
                {salon.imageUrl && (
                  <img src={salon.imageUrl} alt="" className="salon-card-image" />
                )}
                <span className="dot-index salon-card-index">{String(i + 1).padStart(2, "0")}</span>
                <h2 className="salon-card-name">{salon.name}</h2>
                <p className="salon-card-address">{salon.address}</p>
                <span className="salon-card-cta">View salon &rarr;</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
