import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'

const script = readFileSync(new URL('../../wwwroot/js/diary-food.js', import.meta.url), 'utf8')

// Exercise the shipped page script, including selection changes and edit labels.
function page({ name = 'Anonymous food', unit = 'g', isUsda = true,
  portionName = '1 fl oz', amount = 30, quantity = '12', initialName = '',
  portions = [{ id: 10, name: portionName, amount }] } = {}) {
  const elements = new Map()
  function element(id) {
    if (!elements.has(id)) elements.set(id, {
      value: '', textContent: '', dataset: {}, style: {}, checked: false,
      listeners: {}, children: [],
      setAttribute() {}, removeAttribute() {},
      addEventListener(event, callback) { this.listeners[event] = callback },
      appendChild(child) {
        this.children.push(child)
        if (child.selected) this.value = child.value.toString()
      },
      querySelectorAll() { return [] },
    })
    return elements.get(id)
  }
  element('foodId').value = '1'
  element('portionMode').checked = true
  element('portionQuantity').value = quantity
  element('portionSelect').dataset.initialPortionName = initialName
  vm.runInNewContext(script, {
    foods: [{ id: 1, name, unit, isUsda, portions }],
    selectedPortionId: '10',
    Intl,
    document: { getElementById: element, createElement: () => element(Symbol()), addEventListener() {} },
  })
  return element
}

test('USDA unit-sized measures show direct amounts with their own gram weights', () => {
  for (const [portionName, unit, amount, quantity, grams] of [
    ['1 fl oz', 'fl oz', 30, '12', 360],
    ['1 fl oz', 'fl oz', 31, '2.5', 77.5],
    ['1 fl oz', 'fl oz', 29.7, '12', 356.4],
    ['1 cup', 'cup', 244, '0.5', 122],
    ['1 tbsp', 'tbsp', 15, '2', 30],
    ['1 tsp', 'tsp', 5, '3', 15],
    ['  1  FL OZ  ', 'fl oz', 30, '12', 360],
  ]) {
    const el = page({ portionName, amount, quantity })
    assert.equal(el('portionQuantityLabel').textContent, `Amount (${unit})`)
    assert.equal(el('portionSummary').textContent, `${quantity} ${unit} (${grams} g used for nutrition)`)
    assert.equal(el('portionModeLabel').textContent, 'Portion or measure')
  }
})

test('containers, contextual and multi-unit portions retain their full meaning', () => {
  for (const portionName of ['1 can or bottle (12 fl oz)', '2 tbsp', '1 cup, chopped', '1 cup, melted', '1 oz', 'constructor', '__proto__']) {
    const el = page({ portionName, amount: 360, quantity: '0.5' })
    assert.equal(el('portionQuantityLabel').textContent, 'Number of servings')
    assert.equal(el('portionSummary').textContent, `0.5 × ${portionName} (180 g used for nutrition)`)
  }
})

test('food names do not affect presentation and missing measures do not create volume support', () => {
  for (const name of ['Beer', 'Milk', 'Juice', 'Soda', 'Rock']) {
    const el = page({ name, portions: [] })
    assert.equal(el('portionQuantityLabel').textContent, 'Number of servings')
    assert.equal(el('portionSummary').textContent, '')
    assert.equal(el('exactMode').checked, true)
    assert.equal(el('quantityUnit').textContent, 'g')
  }
})

test('CoFID ml and custom foods retain their existing portion display', () => {
  for (const unit of ['ml', 'g']) {
    const el = page({ unit, isUsda: false })
    assert.equal(el('portionQuantityLabel').textContent, 'Number of servings')
    assert.equal(el('portionSummary').textContent, `12 × 1 fl oz (360 ${unit} total)`)
    assert.equal(el('portionModeLabel').textContent, 'Named portion')
  }
})

test('changing measures never merges conflicting gram weights or adds missing units', () => {
  const el = page({ portions: [
    { id: 10, name: '1 fl oz', amount: 30 },
    { id: 11, name: '1 cup', amount: 200 },
    { id: 12, name: '1 can', amount: 356 },
  ] })
  el('portionSelect').value = '11'
  el('portionQuantity').value = '1'
  el('portionSelect').listeners.change()
  assert.equal(el('portionSummary').textContent, '1 cup (200 g used for nutrition)')
  assert.equal(el('portionSelect').children.length, 3)
  el('portionSelect').value = '12'
  el('portionSelect').listeners.change()
  assert.equal(el('portionQuantityLabel').textContent, 'Number of servings')
  assert.equal(el('portionSummary').textContent, '1 × 1 can (356 g used for nutrition)')
})

test('edit uses the historical label and server-supplied historical portion amount', () => {
  const el = page({ initialName: '1 fl oz', portionName: 'renamed portion', amount: 30 })
  assert.equal(el('portionQuantityLabel').textContent, 'Amount (fl oz)')
  assert.equal(el('portionSummary').textContent, '12 fl oz (360 g used for nutrition)')
})

test('invalid and exact-mode quantities do not show a volume summary', () => {
  for (const quantity of ['', '0', '-1', 'NaN', 'Infinity']) {
    assert.equal(page({ quantity })('portionSummary').textContent, '')
  }
  const el = page()
  el('portionMode').checked = false
  el('exactMode').checked = true
  el('exactMode').listeners.change()
  assert.equal(el('portionSummary').textContent, '')
  assert.equal(el('quantityUnit').textContent, 'g')
})

test('preview rounds for display only and does not rewrite submitted values', () => {
  const el = page({ amount: 29.72345, quantity: '0.33' })
  assert.equal(el('portionSummary').textContent, '0.33 fl oz (9.8087 g used for nutrition)')
  assert.equal(el('portionQuantity').value, '0.33')
})

test('empty approximate preview stays hidden', () => {
  const el = page({ portions: [] })
  assert.equal(el('approximationSummary').textContent, '')
  assert.equal(el('approximationSummary').hidden, true)
})
