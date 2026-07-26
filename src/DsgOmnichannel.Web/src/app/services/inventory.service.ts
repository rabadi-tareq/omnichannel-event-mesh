import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface InventoryItem {
  id: string;
  storeId: string;
  productId: string;
  quantity: number;
}

@Injectable({
  providedIn: 'root'
})
export class InventoryService {
  private http = inject(HttpClient);

  getAll(): Observable<InventoryItem[]> {
    return this.http.get<InventoryItem[]>('/api/inventory');
  }

  upsert(storeId: string, productId: string, quantity: number): Observable<InventoryItem> {
    return this.http.post<InventoryItem>('/api/inventory', { storeId, productId, quantity });
  }

  updateQuantity(id: string, quantity: number): Observable<InventoryItem> {
    return this.http.patch<InventoryItem>(`/api/inventory/${id}/quantity`, { quantity });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/inventory/${id}`);
  }
}
