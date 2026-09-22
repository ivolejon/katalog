import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/HomeView.vue'),
      meta: { title: 'Katalog' },
    },
    {
      path: '/labels',
      name: 'labels',
      component: () => import('@/views/LabelsView.vue'),
      meta: { title: 'Labels' },
    },
    {
      path: '/labels/:id',
      name: 'label-detail',
      component: () => import('@/views/LabelDetailView.vue'),
      props: true,
      meta: { title: 'Label' },
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/',
    },
  ],
})

router.afterEach((to) => {
  const base = to.meta.title ? String(to.meta.title) : 'Katalog'
  document.title = to.name === 'home' ? 'Katalog' : `${base} - Katalog`
})

export default router