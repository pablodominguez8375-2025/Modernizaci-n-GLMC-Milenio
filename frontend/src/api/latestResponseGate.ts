/**
 * Guards the private capture detail view against out-of-order network responses.
 * A token is valid only while it is the newest load/selection request. Navigating
 * to another expediente or reloading must invalidate outstanding requests.
 */
export class LatestResponseGate {
  private sequence = 0

  begin(): number {
    return ++this.sequence
  }

  isCurrent(token: number): boolean {
    return token > 0 && token === this.sequence
  }

  invalidate(): void {
    this.sequence += 1
  }
}
