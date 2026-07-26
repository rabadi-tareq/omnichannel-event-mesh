import { Component, OnInit, inject, signal, computed, ElementRef, viewChild, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { SignalRService, OrderJourneyEvent } from '../../signalr.service';
import { InventoryService, InventoryItem } from '../../services/inventory.service';

interface OrderResponse {
  id: string;
  productId: string;
  quantity: number;
  storeId: string;
  status: string;
}

interface ReadyOrder {
  orderId: string;
  displayOrderId: string;
}

@Component({
  selector: 'app-order-journey',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './order-journey.html',
  styleUrl: './order-journey.css'
})
export class OrderJourneyComponent implements OnInit {
  private signalRService = inject(SignalRService);
  private inventoryService = inject(InventoryService);
  private http = inject(HttpClient);

  private logListRef = viewChild<ElementRef<HTMLElement>>('logList');

  // SignalR connection state
  connectionState = this.signalRService.connectionState;

  // Journey event log
  journeyEvents = this.signalRService.orderJourneyEvents;

  // Auto-scroll to bottom when new events arrive
  private scrollEffect = effect(() => {
    const events = this.journeyEvents();
    if (events.length === 0) return;
    const el = this.logListRef()?.nativeElement;
    if (el) {
      setTimeout(() => { el.scrollTop = el.scrollHeight; }, 0);
    }
  });

  // Inventory state
  inventory = signal<InventoryItem[]>([]);
  editingId = signal<string | null>(null);
  editingQuantity = signal<number>(0);

  // Seed form
  newProductId = signal<string>('');
  newQuantity = signal<number>(1);
  seedError = signal<string>('');

  // Order form
  selectedProductId = signal<string>('');
  orderQuantity = signal<number>(1);
  orderError = signal<string>('');
  orderSubmitting = signal<boolean>(false);

  // Tracks placed orders this session: displayOrderId → orderId
  private placedOrders = signal<Record<string, string>>({});

  // Orders that have been allocated and are ready for customer pickup
  readyForPickup = signal<ReadyOrder[]>([]);
  pickupPendingIds = signal<string[]>([]);
  cancelPendingIds = signal<string[]>([]);

  // Number of journey events already processed by the event reactor
  private processedEventCount = 0;

  private eventReactorEffect = effect(() => {
    const events = this.journeyEvents();
    for (let i = this.processedEventCount; i < events.length; i++) {
      this.handleJourneyEvent(events[i]);
    }
    this.processedEventCount = events.length;
  });

  selectedProduct = computed(() =>
    this.inventory().find(i => i.productId === this.selectedProductId())
  );

  ngOnInit(): void {
    this.signalRService.startConnection();
    this.loadInventory();
  }

  private handleJourneyEvent(event: OrderJourneyEvent): void {
    if (event.eventName === 'AllocationConfirmed') {
      const orderId = this.placedOrders()[event.displayOrderId];
      if (orderId) {
        this.readyForPickup.update(list => [...list, { orderId, displayOrderId: event.displayOrderId }]);
      }
      this.loadInventory(); // quantity dropped during allocation — refresh to show it
    } else if (event.eventName === 'InventoryAllocationFailed') {
      this.placedOrders.update(m => {
        const copy = { ...m };
        delete copy[event.displayOrderId];
        return copy;
      });
    } else if (event.eventName === 'OrderPickedUp') {
      this.readyForPickup.update(list => list.filter(o => o.displayOrderId !== event.displayOrderId));
      this.loadInventory();
    } else if (event.eventName === 'InventoryRestored') {
      this.readyForPickup.update(list => list.filter(o => o.displayOrderId !== event.displayOrderId));
      this.cancelPendingIds.update(ids => ids.filter(id => !this.readyForPickup().find(o => o.displayOrderId === event.displayOrderId && o.orderId === id)));
      this.loadInventory(); // quantity restored — refresh to reflect new stock level
    }
  }

  loadInventory(): void {
    this.inventoryService.getAll().subscribe({
      next: items => this.inventory.set(items),
      error: () => {}
    });
  }

  seedProduct(): void {
    const productId = this.newProductId().trim();
    if (!productId || this.newQuantity() < 1) {
      this.seedError.set('Product name and quantity are required.');
      return;
    }
    this.seedError.set('');
    this.inventoryService.upsert('STORE-001', productId, this.newQuantity()).subscribe({
      next: () => {
        this.newProductId.set('');
        this.newQuantity.set(1);
        this.loadInventory();
      },
      error: () => this.seedError.set('Failed to save product.')
    });
  }

  startEdit(item: InventoryItem): void {
    this.editingId.set(item.id);
    this.editingQuantity.set(item.quantity);
  }

  saveEdit(item: InventoryItem): void {
    this.inventoryService.updateQuantity(item.id, this.editingQuantity()).subscribe({
      next: () => {
        this.editingId.set(null);
        this.loadInventory();
      }
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  deleteProduct(item: InventoryItem): void {
    if (!confirm(`Delete '${item.productId}' and all its orders?`)) return;
    this.inventoryService.delete(item.id).subscribe({
      next: () => this.loadInventory()
    });
  }

  placeOrder(): void {
    const product = this.selectedProduct();
    if (!product) {
      this.orderError.set('Please select a product.');
      return;
    }
    if (this.orderQuantity() < 1) {
      this.orderError.set('Quantity must be at least 1.');
      return;
    }
    this.orderError.set('');
    this.orderSubmitting.set(true);

    const displayOrderId = `${this.orderQuantity()} Count of ${product.productId}`;

    this.http.post<OrderResponse>('/api/orders', {
      storeId: 'STORE-001',
      customerName: 'Walk-in Customer',
      productId: product.productId,
      quantity: this.orderQuantity(),
      totalAmount: this.orderQuantity() * 10
    }).subscribe({
      next: (order) => {
        this.orderSubmitting.set(false);
        this.placedOrders.update(m => ({ ...m, [displayOrderId]: order.id }));
        this.orderQuantity.set(1);
      },
      error: () => {
        this.orderSubmitting.set(false);
        this.orderError.set('Failed to place order. Check API connection.');
      }
    });
  }

  confirmPickup(orderId: string): void {
    this.pickupPendingIds.update(ids => [...ids, orderId]);
    this.http.post(`/api/orders/${orderId}/pickup`, { associateId: 'associate-001' }).subscribe({
      next: () => {
        this.pickupPendingIds.update(ids => ids.filter(id => id !== orderId));
      },
      error: () => {
        this.pickupPendingIds.update(ids => ids.filter(id => id !== orderId));
      }
    });
  }

  cancelOrder(orderId: string): void {
    this.cancelPendingIds.update(ids => [...ids, orderId]);
    this.http.post(`/api/orders/${orderId}/cancel`, {}).subscribe({
      next: () => {
        this.cancelPendingIds.update(ids => ids.filter(id => id !== orderId));
        // pickup panel entry is removed when the InventoryRestored SignalR event arrives
      },
      error: () => {
        this.cancelPendingIds.update(ids => ids.filter(id => id !== orderId));
      }
    });
  }

  isPickupPending(orderId: string): boolean {
    return this.pickupPendingIds().includes(orderId);
  }

  isCancelPending(orderId: string): boolean {
    return this.cancelPendingIds().includes(orderId);
  }

  clearLog(): void {
    this.signalRService.clearJourneyEvents();
  }

  exportLog(): void {
    const events = this.journeyEvents();
    const json = JSON.stringify(events, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `order-journey-${new Date().toISOString().replace(/[:.]/g, '-')}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  componentClass(component: string): string {
    if (component.startsWith('API')) return 'tag-api';
    if (component.startsWith('EF') || component.startsWith('SQL')) return 'tag-sql';
    if (component.startsWith('Worker') || component.startsWith('MassTransit')) return 'tag-worker';
    if (component.startsWith('RabbitMQ')) return 'tag-rabbit';
    return 'tag-default';
  }
}
